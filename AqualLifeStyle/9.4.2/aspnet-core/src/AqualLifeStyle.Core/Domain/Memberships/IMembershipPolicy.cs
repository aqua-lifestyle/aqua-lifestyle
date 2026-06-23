namespace AqualLifeStyle.Domain.Memberships
{
    public interface IMembershipPolicy
    {
        bool IsNameUnique(string name);
    }
}
