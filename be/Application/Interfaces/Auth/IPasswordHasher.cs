namespace NX_MTD.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    /// <summary>Hash sai định dạng (vd. placeholder seed) → false, không ném lỗi.</summary>
    bool Verify(string password, string hash);
}
