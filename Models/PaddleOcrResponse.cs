using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Translator_Screen.Models
{
    public class PaddleOcrResponse
    {
        public List<OcrWord>? words { get; set; }

        public class OcrWord
        {
            public string text { get; set; } = string.Empty;
            public OcrBox box { get; set; } = new();
            public float confidence { get; set; }
        }

        public class OcrBox
        {
            public int x { get; set; }
            public int y { get; set; }
            public int w { get; set; }
            public int h { get; set; }
        }
    }
}
