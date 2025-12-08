using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tessera.Agents;
using Tessera.Core.Agents;
using Tessera.Core.Models;
using Tessera.Utils;

namespace Tessera.ViewModels;

public class JsonViewModel : WorkspaceViewModel
{
    private readonly JsonViewAgent _agent;
    private readonly UIToastAgent _toastAgent;
    private readonly TimeSpan _validationDelay = TimeSpan.FromMilliseconds(350);
    private string _editorText = "";
    private string _lastValidText = "";
    private bool _canApply;
    private bool _isDiffOpen;
    private JsonDiffResult? _diffPreview;
    private JsonModel? _pendingModel;
    private string? _validationMessage;
    private CancellationTokenSource? _validationCts;
    private readonly DelegateCommand _confirmApplyCommand;

    public JsonViewModel(DataSyncAgent? dataSyncAgent = null, ValidationAgent? validationAgent = null, JsonAgent? jsonAgent = null, UIToastAgent? toastAgent = null)
    {
        var (table, schema, json, validator, agent) = SampleDataFactory.CreateEmptyWorkspace();
        var sync = dataSyncAgent ?? new DataSyncAgent(table, schema, json, validator, agent);
        var jsonValidator = validationAgent ?? validator;
        var jsonFormatter = jsonAgent ?? agent;
        _agent = new JsonViewAgent(sync, jsonValidator, jsonFormatter);
        _toastAgent = toastAgent ?? new UIToastAgent();

        EditorText = _agent.SerializeCurrent();
        _lastValidText = EditorText;
        CanApply = true;
        DiffPreview = JsonDiffResult.Empty;

        _agent.DataChanged += SyncFromModel;

        ApplyChangesCommand = new DelegateCommand(_ => ShowDiffPreview());
        _confirmApplyCommand = new DelegateCommand(_ => CommitChanges(), _ => _pendingModel is not null);
        ConfirmApplyCommand = _confirmApplyCommand;
        ResetCommand = new DelegateCommand(_ => Reset());
        FormatCommand = new DelegateCommand(_ => Format());


    }

    public override string Title => "JSON View";

    // JSON icon geometry (mdi-code-json)
    // JSON icon geometry (mdi-code-json)
    public override string IconName => "mdi-code-json";

    public override string Subtitle => "Work directly with JSON format";



    public ICommand ApplyChangesCommand { get; }

    public ICommand ConfirmApplyCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand FormatCommand { get; }

    public JsonDiffResult? DiffPreview
    {
        get => _diffPreview;
        private set => SetProperty(ref _diffPreview, value);
    }

    public bool IsDiffOpen
    {
        get => _isDiffOpen;
        set => SetProperty(ref _isDiffOpen, value);
    }

    public bool CanApply
    {
        get => _canApply;
        private set => SetProperty(ref _canApply, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string EditorText
    {
        get => _editorText;
        set
        {
            if (SetProperty(ref _editorText, value))
            {
                ScheduleValidation();
            }
        }
    }

    public event Action<JsonValidationResult>? ValidationChanged;

    private void SyncFromModel()
    {
        // Syncing from model
        EditorText = _agent.SerializeCurrent();
        _lastValidText = EditorText;
        ValidationChanged?.Invoke(new JsonValidationResult(true, new(), _agent.CurrentJson));
    }

    private void ScheduleValidation()
    {
        _validationCts?.Cancel();
        _validationCts = new CancellationTokenSource();
        var token = _validationCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_validationDelay, token);
                await ValidateAsync(EditorText);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }, token);
    }

    private async Task ValidateAsync(string json)
    {
        await Task.Yield();
        var result = _agent.Validate(json);
        CanApply = result.IsValid;
        ValidationMessage = result.IsValid ? "JSON is valid." : result.Errors.FirstOrDefault()?.Message;
        ValidationMessage = result.IsValid ? "JSON is valid." : result.Errors.FirstOrDefault()?.Message;

        ValidationChanged?.Invoke(result);
    }

    private void ShowDiffPreview()
    {
        if (!_agent.TryPrepareDiff(EditorText, out var diff, out var validation, out var model))
        {
            CanApply = false;
            ValidationMessage = validation.Errors.FirstOrDefault()?.Message;
            _toastAgent.ShowToast(ValidationMessage ?? "JSON is invalid", ToastLevel.Error);
            ValidationChanged?.Invoke(validation);
            return;
        }

        DiffPreview = diff;
        _pendingModel = model;
        IsDiffOpen = true;
        _confirmApplyCommand.RaiseCanExecuteChanged();
        ValidationChanged?.Invoke(validation);
    }

    private void CommitChanges()
    {
        if (_pendingModel is null)
        {
            return;
        }

        if (_agent.TryCommit(_pendingModel, out var diff, out var validation))
        {
            _lastValidText = _agent.SerializeCurrent();
            EditorText = _lastValidText;
            CanApply = true;
            DiffPreview = diff;
            _toastAgent.ShowToast("JSON applied successfully", ToastLevel.Success);
        }
        else
        {
            CanApply = false;
            ValidationMessage = validation.Errors.FirstOrDefault()?.Message;
            _toastAgent.ShowToast(ValidationMessage ?? "Failed to apply JSON", ToastLevel.Error);
        }

        IsDiffOpen = false;
        _pendingModel = null;
        _confirmApplyCommand.RaiseCanExecuteChanged();
        ValidationChanged?.Invoke(validation);
    }

    private void Reset()
    {
        EditorText = _lastValidText;
        CanApply = true;
        ValidationMessage = "Restored last valid JSON.";
        _toastAgent.ShowToast("JSON reset", ToastLevel.Info);
        _pendingModel = null;
        _confirmApplyCommand.RaiseCanExecuteChanged();
        IsDiffOpen = false;
        ValidationChanged?.Invoke(new JsonValidationResult(true, new(), _agent.CurrentJson));
    }

    private void Format()
    {
        EditorText = _agent.Format(EditorText);
    }

    public override async Task OnSaveAsync()
    {
        // 1. Validate current text
        var result = _agent.Validate(EditorText);
        if (!result.IsValid)
        {
            var msg = result.Errors.FirstOrDefault()?.Message ?? "Invalid JSON";
            throw new Exception($"Cannot save: {msg}");
        }

        // 2. Prepare Diff/Model
        if (!_agent.TryPrepareDiff(EditorText, out var diff, out var validation, out var model))
        {
             var msg = validation.Errors.FirstOrDefault()?.Message ?? "Invalid JSON structure";
             throw new Exception($"Cannot save: {msg}");
        }

        // 3. Commit to Shared Model
        if (_agent.TryCommit(model!, out _, out _))
        {
            _lastValidText = _agent.SerializeCurrent();
            EditorText = _lastValidText;
            CanApply = true;
            _pendingModel = null;
            IsDiffOpen = false;
        }
        else
        {
            throw new Exception("Failed to commit JSON changes to model.");
        }
        
        await Task.CompletedTask;
    }
}
