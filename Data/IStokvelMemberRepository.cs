// StokvelMember's composite key means IRepository<T>'s GetByIdAsync(Guid id)
// never made sense for it — there is no single id. Our Assignment 5.1
// implementation already sidestepped this by never registering
// IRepository<StokvelMember> at all; access was always raw DbContext queries
// inside EfStokvelRepository. This formalizes that into its own interface
// with a composite-key-aware lookup, used wherever code needs one specific
// membership directly rather than going through the parent Stokvel.
using RondiTrack.Persistence.Entities;

namespace RondiTrack.Data;

public interface IStokvelMemberRepository
{
    Task<StokvelMember?> GetAsync(Guid stokvelId, Guid userId);
    Task<IReadOnlyList<StokvelMember>> GetByStokvelIdAsync(Guid stokvelId);
}