
namespace ToyCompiler.Data;

/// <summary>
/// 指令集
/// </summary>
public enum InsID
{
	stop,//stop the program

	addi, subi, muli, divi, modi,   //addi R1 R2 R3  R1=R2+R3
	gtri, smri, egtri, esmri,       //gtri R1 R2 R3  R1=R2>R3

	addf, subf, mulf, divf, modf,
	gtrf, smrf, egtrf, esmrf,

	and, or,                //and R1 R2 R3  R1=R2&&R3
							//convert
	i2s, i2b, i2f, f2i, //i2s R1 R2		R1 = (short)R2
						//common
	lod,                //lodi R1 num   R1 = num
	cjmp,               //cjmp R1 R2    if(!R1) goto R2;
	jump,               //jump R1       goto R1;
	equ, nqu,           //equ R1 R2 R3  R1=R2==R3
	set,                //set R1 R2     R1=R2
	push,               //push R1       R1 = *stack; stack--; 4byte
	pop,                //pop R1        stack++; *stack=R1;   4byte
	call,               //call R1		push pc+1; junp R1;
	ret,                //ret R1        setstack; push c; jump R20
	ret0,               //ret0			setstack; jump R20
	stsp,               //stsp			R21 = sp
						//pointer
	getpzl,             //getpzl num R1	R1 = &publiczone[num];
	setpz, getpz,       //setpz num R1	publiczone[num] = R1;
	setp4, getp4,       //setp4 R1 R2 R3	*(R1+R2) = R3
	setp4c, getp4c,     //setp4c R1 num R3	*(R1+num) = R3
	setp2, getp2,       //getp4 R1 R2 R3	R3 = *(R1+R2
	setp2c, getp2c,     //getp4c R1	num R3	R3 = *(R1+num)
	setp1, getp1,
	setp1c, getp1c,
	malloc, salloc,     //malloc R1 R2 num  R1 = Malloc(R2*num)
}