using System;

namespace GCCD.AcousticResearch
{
    // Two-phase simplex: maximize c*x subject to A*x<=b, x>=0.
    // Used after enumerating STO's binary peak assignments; no licensed/native solver needed.
    public sealed class StoLinearProgram
    {
        const double Epsilon=1e-8;
        readonly double[,] tableau;
        readonly int[] basis, columns;
        readonly int rows, variables;
        public StoLinearProgram(double[,] a,double[] b,double[] c)
        {
            rows=b.Length; variables=c.Length; tableau=new double[rows+2,variables+2];
            basis=new int[rows]; columns=new int[variables+1];
            for(int i=0;i<rows;i++) { for(int j=0;j<variables;j++) tableau[i,j]=a[i,j]; basis[i]=variables+i; tableau[i,variables]=-1; tableau[i,variables+1]=b[i]; }
            for(int j=0;j<variables;j++) { columns[j]=j; tableau[rows,j]=-c[j]; }
            columns[variables]=-1; tableau[rows+1,variables]=1;
        }
        void Pivot(int row,int col)
        {
            double reciprocal=1/tableau[row,col];
            for(int i=0;i<rows+2;i++) if(i!=row)
                for(int j=0;j<variables+2;j++) if(j!=col) tableau[i,j]-=tableau[row,j]*tableau[i,col]*reciprocal;
            for(int j=0;j<variables+2;j++) if(j!=col) tableau[row,j]*=reciprocal;
            for(int i=0;i<rows+2;i++) if(i!=row) tableau[i,col]*=-reciprocal;
            tableau[row,col]=reciprocal; int old=basis[row];basis[row]=columns[col];columns[col]=old;
        }
        bool Optimize(int objective)
        {
            for(int step=0;step<4096;step++)
            {
                int entering=-1;
                for(int j=0;j<=variables;j++)
                {
                    if(objective==rows && columns[j]==-1) continue;
                    if(entering<0 || tableau[objective,j]<tableau[objective,entering]-Epsilon
                        || Math.Abs(tableau[objective,j]-tableau[objective,entering])<=Epsilon && columns[j]<columns[entering]) entering=j;
                }
                if(entering<0 || tableau[objective,entering]>=-Epsilon) return true;
                int leaving=-1;
                for(int i=0;i<rows;i++) if(tableau[i,entering]>Epsilon)
                {
                    double ratio=tableau[i,variables+1]/tableau[i,entering];
                    if(leaving<0 || ratio<tableau[leaving,variables+1]/tableau[leaving,entering]-Epsilon
                        || Math.Abs(ratio-tableau[leaving,variables+1]/tableau[leaving,entering])<=Epsilon && basis[i]<basis[leaving]) leaving=i;
                }
                if(leaving<0) return false;
                Pivot(leaving,entering);
            }
            return false;
        }
        public bool Solve(out double[] solution,out double optimum)
        {
            solution=null;optimum=0; int row=0;
            for(int i=1;i<rows;i++) if(tableau[i,variables+1]<tableau[row,variables+1]) row=i;
            if(rows>0 && tableau[row,variables+1]<-Epsilon)
            {
                Pivot(row,variables);
                if(!Optimize(rows+1) || Math.Abs(tableau[rows+1,variables+1])>Epsilon) return false;
                for(int i=0;i<rows;i++) if(basis[i]==-1)
                {
                    int col=-1;
                    for(int j=0;j<=variables;j++) if(Math.Abs(tableau[i,j])>Epsilon && (col<0 || columns[j]<columns[col])) col=j;
                    if(col>=0) Pivot(i,col);
                }
            }
            if(!Optimize(rows)) return false;
            solution=new double[variables];
            for(int i=0;i<rows;i++) if(basis[i]>=0 && basis[i]<variables) solution[basis[i]]=Math.Max(0,tableau[i,variables+1]);
            optimum=tableau[rows,variables+1];return true;
        }
    }
}
