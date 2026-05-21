using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Translator_Screen.Models
{
    public class TranslationUploadRequest
    {
        public int UserId { get; set; }
        public int AppSource { get; set; }
        public List<TranslationSessionDto> Sessions { get; set; } = new();
    }

    public class TranslationSessionDto
    {
        public string SourceLanguage { get; set; } = "";
        public string TargetLanguage { get; set; } = "";
        public string ContextName { get; set; } = "";
        public string VideoFilename { get; set; } = "";
        public int VideoDuration { get; set; }
        public List<TranslationRecordDto> Records { get; set; } = new();
    }

    public class TranslationRecordDto
    {
        public string RawInput { get; set; } = "";
        public string TranslateOutput { get; set; } = "";
    }
}
