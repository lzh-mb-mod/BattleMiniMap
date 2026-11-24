namespace BattleMiniMap.View.MapGeometry
{
    internal enum MapPreviewProblem { None, SourceUnavailable, InitializationFailed, CollectionFailed, RenderingFailed }

    // Value-only UI snapshot: never keeps a retired collector or native object alive.
    internal sealed class MapPreviewStatus
    {
        public bool TerrainEnded, TerrainUnavailable, TerrainHasGaps, InitialFinished, Partial;
        public int TerrainPercent, Rejected, Skipped, Unsupported, TerrainFailures;
        // Visibility filtering contributes to Skipped, but is not itself missing coverage.
        public bool HasKnownGaps => Partial || TerrainUnavailable || TerrainHasGaps || Rejected>0 || Unsupported>0 || TerrainFailures>0;
        public string TerrainText => TerrainUnavailable ? "terrain_unavailable" : TerrainEnded ?
            (TerrainHasGaps || TerrainFailures>0 ? "terrain_gaps" : "terrain_ended") : "terrain_reading";
        public string ObjectsText => InitialFinished ? "objects_ended" : TerrainEnded ? "objects_scanning" : "objects_waiting";
        public string CoverageText => HasKnownGaps ? "coverage_gaps" : "coverage_unverified";
        public static string ProblemText(MapPreviewProblem problem)
        {
            switch(problem)
            {
                case MapPreviewProblem.SourceUnavailable: return "source_unavailable";
                case MapPreviewProblem.InitializationFailed: return "initialization_failed";
                case MapPreviewProblem.CollectionFailed: return "collection_failed";
                case MapPreviewProblem.RenderingFailed: return "rendering_failed";
                default: return null;
            }
        }
    }
}
