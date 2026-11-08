namespace RondiTrack.Domain;

// One proof of payment per contribution. The bytes stay in the database: there is no public link to a file.
public class ProofOfPayment
{
    public Guid Id { get; private set; }
    public Guid ContributionId { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public byte[] Content { get; private set; } = [];
    public DateTime UploadedAtUtc { get; private set; }

    private ProofOfPayment() { }

    public ProofOfPayment(Guid contributionId, Guid stokvelId, Guid uploadedBy, string fileName,
                          string contentType, byte[] content, DateTime nowUtc)
    {
        Id = Guid.NewGuid();
        ContributionId = contributionId;
        StokvelId = stokvelId;
        Replace(uploadedBy, fileName, contentType, content, nowUtc);
    }

    public void Replace(Guid uploadedBy, string fileName, string contentType, byte[] content, DateTime nowUtc)
    {
        UploadedByUserId = uploadedBy;
        FileName = fileName;
        ContentType = contentType;
        Content = content;
        SizeBytes = content.LongLength;
        UploadedAtUtc = nowUtc;
    }
}