namespace SpaceBattle.Lib;

public interface IThreadRemoval { Task RemoveThread(string id); }
public partial interface IGameRuntime : IThreadRemoval { }
