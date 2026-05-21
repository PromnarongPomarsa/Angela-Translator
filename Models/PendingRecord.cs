using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Text;

namespace WPF_Translator_Screen.Models
{
    public class PendingRecord
    {
        public int Id { get; set; }
        public string RawInput { get; set; } = string.Empty;
        public string TranslateOutput { get; set; } = string.Empty;
        public string SourceLanguage { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public string AppSource { get; set; } = "0";
        public string? ContextName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSent { get; set; } = false;
    }
}
