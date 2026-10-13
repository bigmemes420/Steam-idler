
using System;
class T {
  const int TableSize=32, Multiplier=16807, Modulus=2147483647, Quotient=127773, Remainder=2836;
  static int seed, current; static int[] table=new int[TableSize];
  static int Next(){
    if(seed<=0 || current==0){
      seed = -seed < 1 ? 1 : -seed;
      for(int index=TableSize+7; index>=0; index--){
        int quotient=seed/Quotient;
        seed=(Multiplier*(seed-(quotient*Quotient)))-(Remainder*quotient);
        if(seed<0) seed+=Modulus;
        if(index<TableSize) table[index]=seed;
      }
      current=table[0];
    }
    int nextQuotient=seed/Quotient;
    seed=(Multiplier*(seed-(nextQuotient*Quotient)))-(Remainder*nextQuotient);
    if(seed<0) seed+=Modulus;
    int slot=current/(1+((Modulus-1)/TableSize));
    if(slot<0 || slot>=TableSize) slot=slot%TableSize & int.MaxValue;
    current=table[slot]; table[slot]=seed; return current;
  }
  static int NextRange(int low,int high){
    int span=high-low+1;
    int limit=int.MaxValue-(int)(((uint)int.MaxValue+1)%(uint)span);
    int sample; do { sample=Next(); } while(sample>limit);
    return low+(sample%span);
  }
  static void Main(){
    int key=129634044; seed = key>=0 ? -key : key; current=0;
    var sb=new System.Text.StringBuilder();
    for(int i=0;i<8;i++) sb.Append(NextRange(32,126)).Append(' ');
    Console.WriteLine(sb.ToString());
  }
}
