using System.Linq.Expressions;

namespace AIStudio.Settings.DataModel;

public sealed class DataDocumentAnalysis(Expression<Func<Data, DataDocumentAnalysis>>? configSelection = null)
{
    /// <summary>
    /// The default constructor for the JSON deserializer.
    /// </summary>
    public DataDocumentAnalysis() : this(null)
    {
    }

    /// <summary>
    /// Configured document analysis policies.
    /// </summary>
    public List<DataDocumentAnalysisPolicy> Policies { get; set; } = [];
}