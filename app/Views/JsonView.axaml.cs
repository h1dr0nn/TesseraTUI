using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using Tessera.Core.Models;
using Tessera.ViewModels;

namespace Tessera.Views;

public partial class JsonView : UserControl
{
    private readonly ErrorLineColorizer _errorColorizer = new();

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
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
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
            .Select(e => e.LineNumber!.Value)
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
