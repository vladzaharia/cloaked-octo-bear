﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using System.Web;
using SasquatchCAIRS.Models;
using SasquatchCAIRS.Models.ServiceSystem;

namespace SasquatchCAIRS.Controllers.ServiceSystem {
    public sealed class RequestManagementController {
        private static readonly RequestManagementController _instance =
            new RequestManagementController();
        private CAIRSDataContext _db = new CAIRSDataContext();

        private RequestManagementController() {
        }

        public static RequestManagementController instance {
            get {
                return _instance;
            }
        }

        /// <summary>
        /// Creates a Request entity based off of the RequestContent.
        /// </summary>
        /// <param name="content">Request content holder.</param>
        /// <returns></returns>
        private Request createRequestEntity(RequestContent content) {
            Request req = new Request();

            if (content.requestID != -1) {
                // Existing Request; has a RequestID
                req.RequestID = content.requestID;
            }

            req.ParentRequestID = content.parentRequestID;

            req.RequestorFName = content.requestorFirstName;
            req.RequestorLName = content.requestorLastName;
            req.RequestorPhone = content.requestorPhoneNum;
            req.RequestorPhoneExt = content.requestorPhoneExt;
            req.RequestorEmail = content.requestorEmail;

            req.PatientFName = content.patientFName;
            req.PatientLName = content.patientLName;

            if (content.patientGender != Constants.Gender.None) {
                req.PatientGender = (byte) content.patientGender;
            } else {
                req.PatientGender = null;
            }

            req.PatientAgencyID = content.patientAgencyID;
            req.PatientAge = content.patientAge;

            req.RequestStatus = (byte) content.requestStatus;
            req.TimeOpened = content.timeOpened;
            req.TimeClosed = content.timeClosed;

            req.RegionID = content.regionID;
            req.RequestorTypeID = content.requestorTypeID;

            return req;
        }

        /// <summary>
        /// Create a QuestionResponse entity based off of the
        /// QuestionResponseContent.
        /// </summary>
        /// <param name="content">QuestionResponse content holder.</param>
        /// <returns></returns>
        private QuestionResponse createQuestionResponseEntity(
            QuestionResponseContent content) {

            QuestionResponse qr = new QuestionResponse();
            qr.RequestID = content.requestID;

            if (content.questionResponseID != -1) {
                // Existing QuestionResponse; has a QuestionResponseID
                qr.QuestionResponseID = content.questionResponseID;
            }

            qr.Question = content.question;
            qr.Response = content.response;
            qr.TimeSpent = content.timeSpent;
            qr.SpecialNotes = content.specialNotes;
            qr.QuestionTypeID = content.questionTypeID;
            qr.TumourGroupID = content.tumourGroupID;

            if (content.severity != Constants.Severity.None) {
                qr.Severity = (byte) content.severity;
            } else {
                qr.Severity = null;
            }

            if (content.consequence != Constants.Consequence.None) {
                qr.Consequence = (byte) content.consequence;
            } else {
                qr.Consequence = null;
            }

            return qr;
        }

        /// <summary>
        /// Create a Reference entity based off of the ReferenceContent.
        /// </summary>
        /// <param name="content">Reference content holder.</param>
        /// <returns></returns>
        private Reference createReferenceEntity(ReferenceContent content) {
            Reference r = new Reference();

            r.RequestID = content.requestID;
            r.QuestionResponseID = content.questionResponseID;

            if (content.referenceID != -1) {
                // Existing Reference; has a ReferenceID
                r.ReferenceID = content.referenceID;
            }

            r.ReferenceType = (byte) content.referenceType;
            r.ReferenceString = content.referenceString;

            return r;
        }

        /// <summary>
        /// Creates a new Request in the database, with corresponding
        /// QuestionResponses and References
        /// </summary>
        /// <param name="reqContent"></param>
        public void create(RequestContent reqContent) {
            try {
                using (TransactionScope trans = new TransactionScope()) {
                    // Insert new Request entity
                    Request req = createRequestEntity(reqContent);
                    _db.Requests.InsertOnSubmit(req);
                    _db.SubmitChanges();

                    // For each QuestionResponse associated with the request
                    foreach (QuestionResponseContent qrContent
                        in reqContent.questionResponseList) {

                        // Set the new RequestID
                        qrContent.requestID = req.RequestID;

                        // Insert new QuestionResponse entity
                        QuestionResponse qr =
                            createQuestionResponseEntity(qrContent);
                        _db.QuestionResponses.InsertOnSubmit(qr);
                        _db.SubmitChanges();

                        // For each Keyword associated with the
                        // QuestionResponse
                        foreach (KeywordContent kwContent
                            in qrContent.keywords) {

                            if (kwContent.keywordId == -1) {                                
                                kwContent.keywordId =
                                    createKeyword(kwContent.keywordStr);
                            }

                            KeywordQuestion kq = new KeywordQuestion();
                            kq.KeywordID = kwContent.keywordId;
                            kq.RequestID = req.RequestID;
                            kq.QuestionResponseID = qr.QuestionResponseID;

                            _db.KeywordQuestions.InsertOnSubmit(kq);
                            _db.SubmitChanges();
                        }

                        // For each Reference associated with the
                        // QuestionResponse
                        foreach (ReferenceContent refContent
                            in qrContent.referenceList) {

                            // Set the new RequestID and
                            // QuestionResponseID
                            refContent.requestID = req.RequestID;
                            refContent.questionResponseID =
                                qr.QuestionResponseID;

                            // Insert new Reference entity
                            _db.References.InsertOnSubmit(
                                createReferenceEntity(refContent));
                            _db.SubmitChanges();
                        }
                    }

                    trans.Complete();
                }
            } catch (Exception ex) {
                // TODO: Do something
            }
        }

        /// <summary>
        /// Create a keyword based off a given keyword string.
        /// </summary>
        /// <param name="keywordStr">Keyword string to create.</param>
        /// <returns>Keyword ID of the new keyword, or existing keyword
        /// ID if already exists.</returns>
        private int createKeyword(string keywordStr) {
            // Keyword may not exist
            Keyword kw;

            try {
                kw = new Keyword();
                kw.KeywordValue = keywordStr;

                _db.Keywords.InsertOnSubmit(kw);
                _db.SubmitChanges();
            } catch (SqlException sqlEx) {
                // Keyword already exists from someone else
                kw = (from kws in _db.Keywords
                      where kws.KeywordValue == keywordStr
                      select kws)
                      .First();
            }

            return kw.KeywordID;
        }

        /// <summary>
        /// Retrieves all of the request information and content from the
        /// database for a given request ID.
        /// </summary>
        /// <param name="requestID"></param>
        /// <returns></returns>
        public RequestContent getRequestDetails(long requestID) {
            Request reqResult =
                (from reqs in _db.Requests
                 where reqs.RequestID == requestID
                 select reqs)
                 .First();

            List<QuestionResponse> qrResults =
                (from qrs in _db.QuestionResponses
                 where qrs.RequestID == requestID
                 orderby qrs.QuestionResponseID
                 select qrs)
                 .ToList();

            List<Reference> refResults =
                (from refs in _db.References
                 where refs.RequestID == requestID
                 orderby refs.QuestionResponseID
                 select refs)
                 .ToList();

            // Create RequestContent holder
            RequestContent reqContent = new RequestContent(reqResult);

            int refCounter = 0;

            // For each QuestionResponse in the database
            foreach (QuestionResponse qr in qrResults) {

                // Create QuestionResponseContent holder
                QuestionResponseContent qrContent =
                    new QuestionResponseContent(qr);
                reqContent.addQuestionResponse(qrContent);

                List<Keyword> kwResults =
                    (from kws in _db.Keywords
                     join kqs in _db.KeywordQuestions
                     on kws.KeywordID equals kqs.KeywordID
                     where kqs.RequestID == requestID &&
                           kqs.QuestionResponseID == qr.QuestionResponseID
                     select kws)
                     .ToList();

                // For each Keyword for the current QuestionResponse
                foreach (Keyword kw in kwResults) {
                    qrContent.addKeyword(new KeywordContent(kw));
                }

                // For each Reference for the current QuestionResponse
                while (refCounter < refResults.Count &&
                    qr.QuestionResponseID ==
                        refResults[refCounter].QuestionResponseID) {

                    // Create ReferenceContent holder
                    ReferenceContent refContent =
                        new ReferenceContent(refResults[refCounter++]);
                    qrContent.addReference(refContent);
                }
            }

            return reqContent;
        }

        /// <summary>
        /// Edit a request properties, question-response pairs and references
        /// for a given request ID.
        /// </summary>
        /// <param name="reqContent">
        /// Request content to modify in the database.
        /// </param>
        public void edit(RequestContent reqContent) {
            try {
                if (reqContent.requestID == null) {
                    throw new Exception("Invalid request.");
                }

                using (TransactionScope trans = new TransactionScope()) {
                    // Check if the request exists
                    int exists =
                        (from reqs in _db.Requests
                         where reqs.RequestID == reqContent.requestID
                         select reqs.RequestID).Count();

                    if (exists < 1) {
                        throw new Exception("Request does not exist.");
                    }

                    // Update the Request entity
                    Request req = createRequestEntity(reqContent);
                    _db.SubmitChanges();

                    // Retrieve the list of current QuestionResponseIDs for
                    // the request
                    List<long> currQRIds =
                        (from qrs in _db.QuestionResponses
                         where qrs.RequestID == reqContent.requestID
                         orderby qrs.QuestionResponseID
                         select qrs.QuestionResponseID)
                         .ToList();

                    // Check all QuestionResponses being updated against
                    // those already in the database
                    foreach (QuestionResponseContent qrContent in
                             reqContent.questionResponseList) {
                        
                        if (currQRIds.Remove(qrContent.questionResponseID)) {
                            // QuestionResponse already exists in the database
                            // Update QuestionResponse, References and Keywords
                            QuestionResponse qr = 
                                createQuestionResponseEntity(qrContent);
                            _db.SubmitChanges();

                            // Retrieve all Keyword IDs for the
                            // given QuestionResponse
                            List<int> currKwIds = getKeywordIds(
                                reqContent.requestID,
                                qrContent.questionResponseID);

                            // Check all Keywords for the QuestionResponse
                            foreach (KeywordContent kwContent in
                                     qrContent.keywords) {
                                if (kwContent.keywordId == -1) {
                                    // Insert new Keyword into database
                                    Keyword kw = new Keyword();
                                    kw.KeywordValue = kwContent.keywordStr;

                                    _db.Keywords.InsertOnSubmit(kw);
                                    _db.SubmitChanges();

                                    kwContent.keywordId = kw.KeywordID;
                                }

                                if (!currKwIds.Remove(kwContent.keywordId)) {
                                    // Set Keyword for QuestionResponse
                                    KeywordQuestion kq = new KeywordQuestion();
                                    kq.KeywordID = kwContent.keywordId;
                                    kq.RequestID = reqContent.requestID;
                                    kq.QuestionResponseID =
                                        qrContent.questionResponseID;

                                    _db.KeywordQuestions.InsertOnSubmit(kq);
                                    _db.SubmitChanges();
                                }
                            }

                            // Remaining Keywords not associated anymore
                            foreach (int kwId in currKwIds) {
                                KeywordQuestion kq =
                                    getKeywordQuestionEntity(
                                        kwId,
                                        reqContent.requestID,
                                        qrContent.questionResponseID);

                                _db.KeywordQuestions.DeleteOnSubmit(kq);
                                _db.SubmitChanges();
                            }

                            // Retrieve all Reference IDs for the given
                            // QuestionResponse
                            List<long> currRefIds = getReferenceIds(
                                reqContent.requestID,
                                qrContent.questionResponseID);

                            // Check all References for the QuestionResponse
                            foreach (ReferenceContent refContent in
                                     qrContent.referenceList) {
                                Reference r = 
                                    createReferenceEntity(refContent);

                                if (refContent.referenceID == -1) {
                                    // Insert new Reference into database
                                    _db.References.InsertOnSubmit(r);
                                } else {
                                    currRefIds.Remove(refContent.referenceID);
                                }

                                _db.SubmitChanges();
                            }

                            // Remaining References do not exist anymore
                            foreach (long refId in currRefIds) {
                                Reference r = getReferenceEntity(
                                    refId,
                                    reqContent.requestID,
                                    qrContent.questionResponseID);

                                _db.References.DeleteOnSubmit(r);
                                _db.SubmitChanges();
                            }
                        } else {
                            // Add new QuestionResponse 
                            QuestionResponse qr =
                                createQuestionResponseEntity(qrContent);

                            _db.QuestionResponses.InsertOnSubmit(qr);
                            _db.SubmitChanges();

                            // Add new Keywords to QuestionResponse
                            foreach (KeywordContent kwContent in
                                     qrContent.keywords) {
                                if (kwContent.keywordId == -1) {
                                    // Insert new Keyword into database
                                    Keyword kw = new Keyword();
                                    kw.KeywordValue = kwContent.keywordStr;

                                    _db.Keywords.InsertOnSubmit(kw);
                                    _db.SubmitChanges();

                                    kwContent.keywordId = kw.KeywordID;
                                }

                                // Set Keyword for QuestionResponse
                                KeywordQuestion kq = new KeywordQuestion();
                                kq.KeywordID = kwContent.keywordId;
                                kq.RequestID = reqContent.requestID;
                                kq.QuestionResponseID =
                                    qrContent.questionResponseID;

                                _db.KeywordQuestions.InsertOnSubmit(kq);
                                _db.SubmitChanges();
                            }

                            // Add all References for the QuestionResponse
                            foreach (ReferenceContent refContent in
                                     qrContent.referenceList) {
                                Reference r =
                                    createReferenceEntity(refContent);

                                _db.References.InsertOnSubmit(r);
                                _db.SubmitChanges();
                            }
                        }
                    }

                    // Remove QuestionResponses that no longer exist
                    foreach (long qrId in currQRIds) {
                        // Delete all Keyword associations
                        List<int> currKwIds = getKeywordIds(
                            reqContent.requestID, qrId);

                        foreach (int kwId in currKwIds) {
                            KeywordQuestion kq = getKeywordQuestionEntity(
                                kwId, reqContent.requestID, qrId);

                            _db.KeywordQuestions.DeleteOnSubmit(kq);
                            _db.SubmitChanges();
                        }

                        // Delete all References
                        List<long> currRefIds = getReferenceIds(
                            reqContent.requestID, qrId);

                        foreach (int refId in currRefIds) {
                            Reference r = getReferenceEntity(
                                refId, reqContent.requestID, qrId);

                            _db.References.DeleteOnSubmit(r);
                            _db.SubmitChanges();
                        }

                        // Delete QuestionResponse
                        QuestionResponse qr = getQuestionResponseEntity(
                            reqContent.requestID, qrId);

                        _db.QuestionResponses.DeleteOnSubmit(qr);
                        _db.SubmitChanges();
                    }

                    trans.Complete();
                }
            } catch (Exception ex) {
                // Do something
            }
        }

        /// <summary>
        /// Retrieve the keyword IDs associated with a given QuestionResponse
        /// in a specific Request.
        /// </summary>
        /// <param name="reqId">Request ID.</param>
        /// <param name="qrId">QuestionResponse ID.</param>
        /// <returns>List of keyword IDs.</returns>
        public List<int> getKeywordIds(long reqId, long qrId) {
            return (from kws in _db.KeywordQuestions
                    where kws.RequestID == reqId &&
                          kws.QuestionResponseID == qrId
                    select kws.KeywordID)
                    .ToList();
        }

        /// <summary>
        /// Return the reference IDs associated with a given QuestionResponse
        /// in a specific Request. 
        /// </summary>
        /// <param name="reqId">Request ID.</param>
        /// <param name="qrId">QuestionResponse ID.</param>
        /// <returns>List of reference IDs.</returns>
        public List<long> getReferenceIds(long reqId, long qrId) {
            return (from refs in _db.References
                    where refs.RequestID == reqId &&
                          refs.QuestionResponseID == qrId
                    select refs.ReferenceID)
                    .ToList();
        }

        /// <summary>
        /// Returns the QuestionResponse entity for a given QuestionResponse.
        /// </summary>
        /// <param name="reqId">Request ID.</param>
        /// <param name="qrId">QuestionResponse ID.</param>
        /// <returns>Specified QuestionResponse entity, or null if does not
        /// exist.</returns>
        public QuestionResponse getQuestionResponseEntity(long reqId,
                                                          long qrId) {
            return (from qrs in _db.QuestionResponses
                    where qrs.RequestID == reqId &&
                          qrs.QuestionResponseID == qrId
                    select qrs)
                    .FirstOrDefault(null);
        }

        /// <summary>
        /// Returns the KeywordQuestion entity for a given Keyword and
        /// QuestionResponse.
        /// </summary>
        /// <param name="kwId">Keyword ID.</param>
        /// <param name="reqId">Request ID.</param>
        /// <param name="qrId">QuestionResponse ID.</param>
        /// <returns>Specified KeywordQuestion entity, or null if does
        /// not exist.</returns>
        public KeywordQuestion getKeywordQuestionEntity(int kwId,
                                                        long reqId,
                                                        long qrId) {
            return (from kqs in _db.KeywordQuestions
                    where kqs.KeywordID == kwId &&
                          kqs.RequestID == reqId &&
                          kqs.QuestionResponseID == qrId
                    select kqs)
                    .FirstOrDefault(null);
        }

        /// <summary>
        /// Returns the Reference entity for a given Reference and 
        /// QuestionResponse.
        /// </summary>
        /// <param name="refId">Reference ID.</param>
        /// <param name="reqId">Request ID.</param>
        /// <param name="qrId">QuestionResponse ID.</param>
        /// <returns>Specific Reference entity, or null if does not exist.
        /// </returns>
        public Reference getReferenceEntity(long refId,
                                            long reqId,
                                            long qrId) {
            return (from refs in _db.References
                    where refs.RequestID == reqId &&
                          refs.QuestionResponseID == qrId
                    select refs)
                    .FirstOrDefault(null);
        }

        /// <summary>
        /// Mark a request with the given request ID as invalid in the database.
        /// </summary>
        /// <param name="requestID">Request ID.</param>
        public void invalidate(long requestID) {
            try {
                using (TransactionScope trans = new TransactionScope()) {
                    Request req = (from reqs in _db.Requests
                                   where reqs.RequestID == requestID
                                   select reqs).First();
                    req.RequestStatus = (byte) Constants.RequestStatus.Invalid;
                    _db.SubmitChanges();

                    trans.Complete();
                }
            } catch (Exception ex) {
                // TODO: Do something
            }
        }
    }
}