namespace HomeGenie.Service.Updates
{
    public class ArchiveDownloadEventArgs
    {
        public ArchiveDownloadStatus Status;
        public ReleaseInfo ReleaseInfo;

        public ArchiveDownloadEventArgs(ReleaseInfo releaseInfo, ArchiveDownloadStatus status)
        {
            this.ReleaseInfo = releaseInfo;
            this.Status = status;
        }
    }
}
