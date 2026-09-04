
namespace ToyCompiler.Data;

/// <summary>
/// 用于执行的程序
/// </summary>
public struct CompiledProgram(int stklen, int clen, Ins[] ilist, int varcount, int[] constdata)
{
	/// <summary>
	/// 公共变量数
	/// </summary>
	public int PvarCount = varcount;
	/// <summary>
	/// 栈深度
	/// </summary>
	public int StackLen = stklen;
	/// <summary>
	/// 调用栈深度
	/// </summary>
	public int CallStackLen = clen;
	/// <summary>
	/// 指令表
	/// </summary>
	public Ins[] InsList = ilist;
	/// <summary>
	/// 常量区。存有常量字符串
	/// </summary>
	public int[] ConstDat = constdata;
}
