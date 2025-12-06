using Tessera.Core.Agents;
using Tessera.Core.Models;

namespace Tessera.Agents;

public class JsonViewAgent
{
    private readonly DataSyncAgent _dataSyncAgent;
    private readonly ValidationAgent _validationAgent;
    private readonly JsonAgent _jsonAgent;

    public JsonViewAgent(DataSyncAgent dataSyncAgent, ValidationAgent validationAgent, JsonAgent jsonAgent)
    {
        _dataSyncAgent = dataSyncAgent;
        _validationAgent = validationAgent;
        _jsonAgent = jsonAgent;
    }

    public SchemaModel Schema => _dataSyncAgent.Schema;

    public JsonModel CurrentJson => _dataSyncAgent.Json;

    public JsonValidationResult Validate(string jsonText) => _validationAgent.ValidateJsonText(jsonText, _dataSyncAgent.Schema);

    public bool TryPrepareDiff(string jsonText, out JsonDiffResult diff, out JsonValidationResult validation, out JsonModel? model)
    {
        validation = _validationAgent.ValidateJsonText(jsonText, _dataSyncAgent.Schema);
        model = validation.Model;
        if (!validation.IsValid || model is null)
        {
            diff = JsonDiffResult.Empty;
            return false;
        }

        diff = _jsonAgent.BuildDiff(_dataSyncAgent.Json, model, _dataSyncAgent.Schema);
        return true;
    }

    public bool TryCommit(JsonModel model, out JsonDiffResult diff, out JsonValidationResult validation)
    {
        return _dataSyncAgent.TryApplyJson(model, out diff, out validation);
    }

    public string Serialize(JsonModel model) => _jsonAgent.Serialize(model);

    public string SerializeCurrent() => _jsonAgent.Serialize(_dataSyncAgent.Json);

    public string Format(string jsonText) => _jsonAgent.Format(jsonText);
}
