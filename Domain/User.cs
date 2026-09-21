//The User entity. Validates its own name/email, controls active/inactive state,
//invalid users can't be constructed.
namespace RondiTrack.Domain;

public class User
{
    public Guid Id { get; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }

    public User(string fullName, string email)
    {
        Id = Guid.NewGuid();
        SetFullName(fullName);
        SetEmail(email);
        IsActive = true;
    }

    public void UpdateDetails(string fullName, string email)
    {
        SetFullName(fullName);
        SetEmail(email);
    }

    public void Deactivate() => IsActive = false;
   
    private void SetFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = fullName.Trim();
    }

    private void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("A valid email is required.", nameof(email));
        Email = email.Trim().ToLowerInvariant();
    }
}