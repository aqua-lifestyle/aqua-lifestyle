using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Web.Host.Models
{
    public class ErrorResponse
    {
        // CorrelationId to tie client responses to server logs
        public string CorrelationId { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> FieldErrors { get; set; } = new Dictionary<string, string>();
    }
}
