

namespace ToyCompiler.Data;

/// <summary>
/// 用于执行的命令
/// </summary>
public struct Ins(InsID id, int p1, int p2, int p3)
{
	public InsID ID = id;
	public int Para1 = p1;
	public int Para2 = p2;
	public int Para3 = p3;

	public readonly override string ToString()
	{
		if (ID.ToString().StartsWith("lod"))
		{
			return " " + ID.ToString() + " R" + Para1 + " " + Para2 + " ";
		}
		return " " + ID.ToString() + " R" + Para1 + " R" + Para2 + " R" + Para3 + " ";
	}
}