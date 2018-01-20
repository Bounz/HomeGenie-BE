namespace HomeGenie.Service.Updates
{
    public class UpdateProgressEventArgs
    {
        public UpdateProgressStatus Status;

        public UpdateProgressEventArgs(UpdateProgressStatus status)
        {
            this.Status = status;
        }
    }
}
