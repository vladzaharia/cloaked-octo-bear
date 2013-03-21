﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SasquatchCAIRS.Models;

namespace SasquatchCAIRS.Controllers {
    [Authorize(Roles = "Administrator")]
    public class AuditLogManagementController : Controller {
        private CAIRSDataContext _db = new CAIRSDataContext();

        /// <summary>
        ///     Add entry to audit log table when an AuditType action is performed on a request.
        /// </summary>
        /// <param name="requestId">The ID of the Request</param>
        /// <param name="userId">The ID of the User</param>
        /// <param name="type">The type of audit action</param>
        public void addEntry(long requestId, int userId,
                             Constants.AuditType type) {
            // Create a new entry in the log, using the Request ID, user ID, and action provided.
            // Set to the current datetime
            _db.AuditLogs.InsertOnSubmit(new AuditLog {
                RequestID = requestId,
                UserID = userId,
                AuditType = (byte) type,
                AuditDate = DateTime.Now
            });

            // Submit to DB.
            _db.SubmitChanges();
        }

        /// <summary>
        ///     Create an audit report with all AuditLog entries for a specified request ID.
        /// </summary>
        /// <param name="request">The request to track activitiy for</param>
        /// <param name="exportFilePath">The filepath selected by the user to save the report in </param>
        public void createReportForRequest(Request request) {
            // Create blank list of AuditLogs and fill with all AuditLogs for the given request ID
            List<AuditLog> requestLogs =
                (from r in _db.AuditLogs
                 where r.RequestID == request.RequestID
                 select r).ToList();

            // Create DataTable with requestLogs to send to XLSExporter
            var xlsExport =
                new DataTable("Audit Log for Request " + request.RequestID);

            // Create required columns
            xlsExport.Columns.Add("User Action", typeof (string));
            xlsExport.Columns.Add("Action Timestamp", typeof (DateTime));
            xlsExport.Columns.Add("User", typeof (string));

            // Populate table rows
            requestLogs.ForEach(
                delegate(AuditLog a) {
                    xlsExport.Rows.Add(a.AuditType.ToString(), a.AuditDate,
                                       a.UserID);
                });

            // Call XLSExporter
            //ExcelExportController.ExportAuditLogTable(xlsExport, exportFilePath);
        }

        /// <summary>
        ///     Create an audit report with all AuditLog entries for a specified user in a specified date range.
        /// </summary>
        /// <param name="userID">The user ID to track activitiy for</param>
        /// <param name="startDate"> The start of the specified date range</param>
        /// <param name="endDate">The end of the specified date range</param>
        /// <param name="exportFilePath">The filepath selected by the user to save the report in </param>
        public void createReportForUser(int userID, DateTime startDate,
                                        DateTime endDate) {
            // Create blank list of AuditLogs and fill with all AuditLogs for the given request ID
            var requestLogs = new List<AuditLog>();
            foreach (
                AuditLog log in
                    (_db.AuditLogs.Where(
                        r =>
                        r.UserID == userID && 
                        r.AuditDate.Date > startDate.Date &&
                        r.AuditDate.Date < endDate.Date))) {
                requestLogs.Add(log);
            }

            // Create DataTable with requestLogs to send to XLSExporter
            var xlsExport =
                new DataTable("Audit Log for User " + userID.ToString() +
                              "Between "
                              + startDate.Date.ToLongDateString() + "and " +
                              endDate.Date.ToLongDateString());

            // Create required columns
            xlsExport.Columns.Add("User Action", typeof (string));
            xlsExport.Columns.Add("Request ID", typeof (long));
            xlsExport.Columns.Add("Action Timestamp", typeof (DateTime));

            // Populate table rows
            requestLogs.ForEach(delegate(AuditLog a) {
                // TODO: Formatting on AuditDate may need to be corrected once Hanna's exporter can be tested
                xlsExport.Rows.Add(a.AuditType.ToString(),
                                   a.RequestID.ToString(), a.AuditDate);
            });

            // Call XLSExporter with table
            // TODO: determine file paths to be passed.
            //ExcelExportController.ExportAuditLogTable(xlsExport, exportFilePath);
        }
    }
}