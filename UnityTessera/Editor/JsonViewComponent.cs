using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Editor
{
    public class JsonViewComponent
    {
        private TesseraEditorState _state;
        private string _jsonText = "";
        private string _errorMessage = "";
        private Vector2 _scrollPosition;
        
        // These methods are now called from TesseraWindow's IMGUI buttons
        public void FormatJson() => FormatJsonInternal();
        public void ApplyJson() => ApplyJsonInternal();
        
        public JsonViewComponent(TesseraEditorState state)
        {
            _state = state;
        }
        
        public void DrawGUI()
        {
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                
                // Use TextArea for JSON editing with a monospaced font style if possible (Unity default is acceptable)
                // We'll calculate a height based on content or a min height
                var style = EditorStyles.textArea;
                
                // Allow the text area to expand
                _jsonText = EditorGUILayout.TextArea(_jsonText, style, GUILayout.ExpandHeight(true));
            }
        }
        
        public void Refresh()
        {
            if (_state.Table == null || _state.Schema == null)
            {
                _jsonText = "[]";
                return;
            }
            
            var jsonAgent = new JsonAgent();
            var jsonModel = jsonAgent.BuildJsonFromTable(_state.Table, _state.Schema);
            _jsonText = jsonAgent.Serialize(jsonModel);
            _errorMessage = "";
        }
        
        private void FormatJsonInternal()
        {
            try
            {
                var jsonAgent = new JsonAgent();
                var parseResult = jsonAgent.Parse(_jsonText);
                
                if (parseResult.IsValid && parseResult.Model != null)
                {
                    _jsonText = jsonAgent.Serialize(parseResult.Model);
                    _errorMessage = "";
                    GUI.FocusControl(null); // Unfocus to update display if needed
                }
                else
                {
                    _errorMessage = $"Invalid JSON: {parseResult.ErrorMessage}";
                }
            }
            catch (System.Exception ex)
            {
                _errorMessage = $"Format error: {ex.Message}";
            }
        }
        
        private void ApplyJsonInternal()
        {
            try
            {
                if (_state.Schema == null)
                {
                    _errorMessage = "Schema is required to apply JSON changes";
                    return;
                }
                
                var jsonAgent = new JsonAgent();
                var parseResult = jsonAgent.Parse(_jsonText);
                
                if (parseResult.IsValid && parseResult.Model != null)
                {
                    _state.Table = jsonAgent.BuildTableFromJson(parseResult.Model, _state.Schema);
                    _errorMessage = "";
                    Debug.Log("[Tessera] JSON changes applied to table");
                }
                else
                {
                    var errorMsg = parseResult.ErrorMessage ?? "Unknown error";
                    var lineInfo = parseResult.LineNumber.HasValue ? $" at line {parseResult.LineNumber}" : "";
                    _errorMessage = $"Invalid JSON: {errorMsg}{lineInfo}";
                }
            }
            catch (System.Exception ex)
            {
                _errorMessage = $"Apply error: {ex.Message}";
            }
        }
    }
}
