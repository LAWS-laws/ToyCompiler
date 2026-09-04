using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 将存于寄存器的变量,name,type,state
/// </summary>
public class RegVar(string nam, VarType typ, RegStat state, int off)
{
	public string Name = nam;
	public VarType Type = typ;
	public RegStat State = state;
	public readonly int Offest = off;//寄存器编号

	public void Update(string nam, VarType typ, RegStat state)
	{
		State = state;
		Name = nam;
		Type = typ;
	}
	public override string ToString()
	{
		return Type.ToString() + " - " + Name;
	}
}