namespace Tessera.ViewModels;

public class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        Table = new TableViewModel();
        Schema = new SchemaViewModel();
        Json = new JsonViewModel();
    }

    public TableViewModel Table { get; }

    public SchemaViewModel Schema { get; }

    public JsonViewModel Json { get; }
}
