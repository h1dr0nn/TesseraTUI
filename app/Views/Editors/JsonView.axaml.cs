using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Indentation;
using Tessera.Core.Models;
using Tessera.ViewModels;

namespace Tessera.Views.Editors;

public partial class JsonView : UserControl
{
    private readonly ErrorLineColorizer _errorColorizer = new();
    private bool _isSyncing;

    public JsonView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is JsonViewModel vm)
        {
            vm.ValidationChanged += ApplyValidation;
        }

        if (this.FindControl<TextEditor>("JsonEditor") is { } editor)
        {
            editor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();
            ApplyThemeColors(editor);
            editor.TextChanged += OnEditorTextChanged;

            if (DataContext is JsonViewModel viewModel)
            {
                // Syncing text

                _isSyncing = true;
                editor.Text = viewModel.EditorText;
                _isSyncing = false;
            }
        }
        
        // Listen for theme changes
        if (Avalonia.Application.Current is { } app)
        {
            app.ActualThemeVariantChanged += OnThemeChanged;
        }
    }
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (this.FindControl<TextEditor>("JsonEditor") is { } editor)
        {
            ApplyThemeColors(editor);
        }
    }
    
    private void ApplyThemeColors(TextEditor editor)
    {
        var isDark = Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ||
                     Avalonia.Application.Current?.ActualThemeVariant?.Key?.ToString()?.Contains("Dark") == true;
        
        // Remove built-in highlighting, we'll use our custom colorizer
        editor.SyntaxHighlighting = null;
        
        // Remove existing JSON colorizer
        var existing = editor.TextArea.TextView.LineTransformers.OfType<JsonSyntaxColorizer>().ToList();
        foreach (var item in existing)
        {
            editor.TextArea.TextView.LineTransformers.Remove(item);
        }
        
        // Add custom JSON colorizer
        editor.TextArea.TextView.LineTransformers.Insert(0, new JsonSyntaxColorizer(isDark));
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is JsonViewModel vm)
        {
            vm.ValidationChanged += ApplyValidation;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            
            if (this.FindControl<TextEditor>("JsonEditor") is { } editor)
            {
               // Initial sync

               _isSyncing = true;
               editor.Text = vm.EditorText;
               _isSyncing = false;
            }
            else 
            {
                // JsonEditor control not found in OnDataContextChanged

            }
        }
    }

    private void OnEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_isSyncing) return;
        
        if (DataContext is JsonViewModel vm && sender is TextEditor editor)
        {
            _isSyncing = true;
            vm.EditorText = editor.Text;
            _isSyncing = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncing) return;

        if (e.PropertyName == nameof(JsonViewModel.EditorText))
        {

            if (DataContext is JsonViewModel vm && this.FindControl<TextEditor>("JsonEditor") is { } editor)
            {
                if (editor.Text != vm.EditorText)
                {

                    _isSyncing = true;
                    editor.Text = vm.EditorText;
                    _isSyncing = false;
                }
            }
        }
    }

    private void ApplyValidation(JsonValidationResult result)
    {
        if (this.FindControl<TextEditor>("JsonEditor") is not { } editor)
        {
            return;
        }

        editor.TextArea.TextView.LineTransformers.Remove(_errorColorizer);
        _errorColorizer.Update(result.Errors);
        editor.TextArea.TextView.LineTransformers.Add(_errorColorizer);
    }
}

internal class ErrorLineColorizer : DocumentColorizingTransformer
{
    private HashSet<int> _errorLines = new();
    private bool _hasErrors;

    public void Update(IEnumerable<JsonValidationError> errors)
    {
        _errorLines = errors
            .Where(e => e.LineNumber.HasValue)
            .Select(e => (int)e.LineNumber!.Value)
            .ToHashSet();
        _hasErrors = errors.Any();
    }

    protected override void ColorizeLine(AvaloniaEdit.Document.DocumentLine line)
    {
        if (!_hasErrors)
        {
            return;
        }

        if (_errorLines.Count == 0 || _errorLines.Contains(line.LineNumber))
        {
            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.Parse("#22E45858")));
                element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#E45858")));
            });
        }
    }
}

internal class JsonSyntaxColorizer : DocumentColorizingTransformer
{
    private readonly bool _isDark;
    
    // Dark mode colors (brighter, easier to read on dark background)
    private static readonly SolidColorBrush DarkKeyBrush = new(Color.Parse("#9CDCFE"));      // Light blue for keys
    private static readonly SolidColorBrush DarkStringBrush = new(Color.Parse("#CE9178"));  // Light orange for strings
    private static readonly SolidColorBrush DarkNumberBrush = new(Color.Parse("#B5CEA8"));  // Light green for numbers
    private static readonly SolidColorBrush DarkBoolBrush = new(Color.Parse("#569CD6"));    // Blue for true/false/null
    private static readonly SolidColorBrush DarkBraceBrush = new(Color.Parse("#D4D4D4"));   // Light gray for braces
    
    // Light mode colors (darker, easier to read on light background)
    private static readonly SolidColorBrush LightKeyBrush = new(Color.Parse("#0451A5"));    // Dark blue for keys
    private static readonly SolidColorBrush LightStringBrush = new(Color.Parse("#A31515")); // Dark red for strings
    private static readonly SolidColorBrush LightNumberBrush = new(Color.Parse("#098658")); // Green for numbers
    private static readonly SolidColorBrush LightBoolBrush = new(Color.Parse("#0000FF"));   // Blue for true/false/null
    private static readonly SolidColorBrush LightBraceBrush = new(Color.Parse("#000000"));  // Black for braces

    public JsonSyntaxColorizer(bool isDark)
    {
        _isDark = isDark;
    }

    protected override void ColorizeLine(AvaloniaEdit.Document.DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        int offset = line.Offset;
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            // Skip whitespace
            if (char.IsWhiteSpace(c)) continue;
            
            // Braces and brackets
            if (c is '{' or '}' or '[' or ']' or ',' or ':')
            {
                ChangeLinePart(offset + i, offset + i + 1, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(_isDark ? DarkBraceBrush : LightBraceBrush);
                });
                continue;
            }
            
            // Strings (keys or values)
            if (c == '"')
            {
                int start = i;
                i++; // Skip opening quote
                while (i < text.Length && !(text[i] == '"' && (i == 0 || text[i - 1] != '\\')))
                {
                    i++;
                }
                int end = i < text.Length ? i + 1 : i; // Include closing quote
                
                // Check if this is a key (followed by ':') or a value
                bool isKey = false;
                for (int j = end; j < text.Length; j++)
                {
                    if (char.IsWhiteSpace(text[j])) continue;
                    isKey = text[j] == ':';
                    break;
                }
                
                var brush = isKey 
                    ? (_isDark ? DarkKeyBrush : LightKeyBrush)
                    : (_isDark ? DarkStringBrush : LightStringBrush);
                
                ChangeLinePart(offset + start, offset + end, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                });
                continue;
            }
            
            // Numbers
            if (char.IsDigit(c) || (c == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                int start = i;
                if (c == '-') i++;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == 'e' || text[i] == 'E' || text[i] == '+' || text[i] == '-'))
                {
                    i++;
                }
                ChangeLinePart(offset + start, offset + i, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(_isDark ? DarkNumberBrush : LightNumberBrush);
                });
                i--; // Adjust for loop increment
                continue;
            }
            
            // true, false, null
            if (text.Length - i >= 4)
            {
                var remaining = text.Substring(i);
                string? keyword = null;
                if (remaining.StartsWith("true")) keyword = "true";
                else if (remaining.StartsWith("false")) keyword = "false";
                else if (remaining.StartsWith("null")) keyword = "null";
                
                if (keyword != null)
                {
                    ChangeLinePart(offset + i, offset + i + keyword.Length, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(_isDark ? DarkBoolBrush : LightBoolBrush);
                    });
                    i += keyword.Length - 1;
                }
            }
        }
    }
}
