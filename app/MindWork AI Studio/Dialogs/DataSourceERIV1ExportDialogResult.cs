using AIStudio.Settings.DataModel;

namespace AIStudio.Dialogs;

public readonly record struct DataSourceERIV1ExportDialogResult(bool IncludeSecret, DataSourceERIUsernamePasswordMode UsernamePasswordMode);