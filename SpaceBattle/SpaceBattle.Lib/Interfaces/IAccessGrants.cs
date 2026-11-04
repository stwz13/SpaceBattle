namespace SpaceBattle.Lib;

public interface IAccessGrants
{
    bool Contains(string subject, string action, string scope);
}
