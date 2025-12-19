using UnityEditor;
using UnityEngine;

namespace Tessera.Editor
{
    public static class TesseraSettings
    {
        private const string KEY_PREFIX = "Tessera_";

        // Editor Settings
        public static int FontSize
        {
            get => EditorPrefs.GetInt(KEY_PREFIX + "FontSize", 14);
            set => EditorPrefs.SetInt(KEY_PREFIX + "FontSize", value);
        }

        public static int RowHeight
        {
            get => EditorPrefs.GetInt(KEY_PREFIX + "RowHeight", 0);
            set => EditorPrefs.SetInt(KEY_PREFIX + "RowHeight", value);
        }

        public static bool AutoSave
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "AutoSave", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "AutoSave", value);
        }

        public static bool ShowLineNumbers
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "ShowLineNumbers", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "ShowLineNumbers", value);
        }

        public static bool WordWrap
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "WordWrap", false);
            set => EditorPrefs.SetBool(KEY_PREFIX + "WordWrap", value);
        }

        // Data Processing Settings
        public static string CsvDelimiter
        {
            get => EditorPrefs.GetString(KEY_PREFIX + "CsvDelimiter", "Comma (,)");
            set => EditorPrefs.SetString(KEY_PREFIX + "CsvDelimiter", value);
        }

        public static bool TrimWhitespace
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "TrimWhitespace", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "TrimWhitespace", value);
        }

        public static bool ArrayDisplayMultiLine
        {
            get => EditorPrefs.GetBool(KEY_PREFIX + "ArrayDisplayMultiLine", true);
            set => EditorPrefs.SetBool(KEY_PREFIX + "ArrayDisplayMultiLine", value);
        }
        
        // Helper to get actual char from verbose delimiter string
        public static char GetDelimiterChar()
        {
            string d = CsvDelimiter;
            if (d.Contains("Semicolon")) return ';';
            if (d.Contains("Tab")) return '\t';
            if (d.Contains("Pipe")) return '|';
            return ',';
        }

        public static readonly string[] AvailableDelimiters = new[] 
        { 
            "Comma (,)", 
            "Semicolon (;)", 
            "Tab (\\t)", 
            "Pipe (|)" 
        };
        
        public static readonly int[] AvailableFontSizes = new[] { 10, 11, 12, 13, 14, 16, 18, 20, 24 };
    }
}
