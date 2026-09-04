using ToyCompiler.Data;

namespace ToyCompiler.Compiler;

/// <summary>
/// 将存于寄存器的变量,name,type,state
/// </summary>
public class RegVar(string nam, VarType typ, RegStat state, int off)
{
	/// <summary>
	/// 变量名
	/// </summary>
	public string Name = nam;
	/// <summary>
	/// 变量类型
	/// </summary>
	public VarType Type = typ;
	/// <summary>
	/// 存储器状态（已占用或未使用）
	/// </summary>
	public RegStat State = state;
	/// <summary>
	/// 寄存器编号
	/// </summary>
	public readonly int Offest = off;

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