using ArcFlow.Features.YouTubePlayer.ImportExport;

namespace ArcFlow.Features.YouTubePlayer.State;

public abstract record ImportExportState
{
    private ImportExportState() { }

    public sealed record Idle : ImportExportState;
    public sealed record ExportInProgress : ImportExportState;
    public sealed record ExportSucceeded(DateTime ExportedAtUtc) : ImportExportState;
    public sealed record ExportFailed(ExportError Error) : ImportExportState;
    public sealed record ImportParsing : ImportExportState;
    public sealed record ImportParsed(ExportEnvelopeV1 Envelope) : ImportExportState;
    public sealed record ImportValidated(ExportEnvelopeV1 Envelope) : ImportExportState;
    public sealed record ImportApplied : ImportExportState;
    public sealed record ImportSucceeded(int PlaylistCount, int VideoCount) : ImportExportState;
    public sealed record ImportFailed(ImportError Error) : ImportExportState;
}
