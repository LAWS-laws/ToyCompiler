
using ToyCompiler.Compiler;
using System.Text;

namespace ToyCompiler.Data;

/// <summary>
/// 变量的类型
/// </summary>
public struct VarType
{
	public TypeID ID;
	public int Pdepth;
	public VarType(TypeID id)
	{
		ID = id;
		Pdepth = 0;
	}
	public VarType(TypeID id, int depth)
	{
		ID = id;
		Pdepth = depth;
	}

	/// <summary>
	/// 是否与指定的 <see cref="VarType"/> 对象相等
	/// </summary>
	public readonly bool Equ(VarType varType)
	{
		return ID == varType.ID && Pdepth == varType.Pdepth;
	}
	/// <summary>
	/// 是否与指定的 <see cref="VarType"/> 对象相等
	/// </summary>
	public readonly bool Equ(TypeID id)
	{
		return ID == id && Pdepth == 0;
	}

	public readonly override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		if (ID <= TypeID.Bool) sb.Append(ID.ToString());
		else sb.Append(PublicTokens.GetUserType(ID).Name);

		for (int i = 0; i < Pdepth; i++) sb.Append("[]");
		return sb.ToString();
	}
}