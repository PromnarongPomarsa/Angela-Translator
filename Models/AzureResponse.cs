using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_Translator_Screen.Models
{
    public class AzureResponse
    {
        public List<translations> translations { get ; set;  } 

    }

    public class translations
    {
        public string text { get; set; }
        public string to { get; set; }
    }


}
