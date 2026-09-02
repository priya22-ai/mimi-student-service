namespace WebApplication1.Models
{
    public enum RequestType
    {
        IDCardReplacement,
        TranscriptRequest,
        CertificateRequest
    }

    public enum RequestStatus
    {
        Pending,
        Processing,
        Completed,
        Rejected
    }
}
