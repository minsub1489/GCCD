using System;
using System.Linq;
namespace GCCD.Dts
{
    public sealed class Response
    {
        public double Bearing {get;private set;}public DistanceBin Bin {get;private set;}
        public Response(double bearing,DistanceBin bin){if(!Enum.IsDefined(typeof(DistanceBin),bin))throw new ArgumentException("Unknown distance bin.");Bearing=Angles.Normalize(bearing);Bin=bin;}
    }
    public sealed class Score
    {
        public double JointAccuracy,DirectionAccuracy,DistanceAccuracy,FalseResponseRate,NoResponseRate;
        public double? Precision,DirectionError;
        public bool Ambiguous;public bool[] JointCorrect;public int[] MatchedThreatIndices;public int[,] Confusion=new int[3,3];
    }
    public static class ResponseScorer
    {
        public static Score Score(Threat[] original, Response[] responses, double tolerance, bool exclusiveBoundary = false)
        {
            Angles.Finite(tolerance);
            if (original == null || original.Length == 0 || responses == null ||
                responses.Length > original.Length || tolerance < 0 || tolerance > 180)
                throw new ArgumentException("Valid threats, responses and direction tolerance required.");

            int n = original.Length, m = responses.Length;
            var score = new Score { MatchedThreatIndices = new int[m], JointCorrect = new bool[m], NoResponseRate = m == 0 ? 1 : 0 };
            if (m == 0) return score;

            var bins = original.Select(t => DistanceEncoding.Bin(t.Distance)).ToArray();
            var joint = new double[m, n];
            var direction = new double[m, n];
            var distance = new double[m, n];
            var errors = new double[m, n];
            // One extra correct pair outranks the largest possible difference in total angular error.
            double penalty = 180.0 * m + 1;
            for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
            {
                errors[i, j] = Angles.Difference(original[j].Bearing, responses[i].Bearing);
                direction[i, j] = (errors[i, j] == 0 || (exclusiveBoundary ? errors[i, j] < tolerance : errors[i, j] <= tolerance)) ? 0 : 1;
                distance[i, j] = bins[j] == responses[i].Bin ? 0 : 1;
                joint[i, j] = errors[i, j] + (direction[i, j] + distance[i, j] == 0 ? 0 : penalty);
            }

            int[] match = Assign(joint);
            score.MatchedThreatIndices = match;
            int correct = 0;
            double totalError = 0;
            for (int i = 0; i < m; i++)
            {
                int j = match[i];
                score.JointCorrect[i] = joint[i, j] < penalty;
                if (score.JointCorrect[i]) correct++;
                totalError += errors[i, j];
                score.Confusion[(int)bins[j], (int)responses[i].Bin]++;
            }
            score.JointAccuracy = correct / (double)n;
            score.DirectionAccuracy = Assign(direction).Where((j, i) => direction[i, j] == 0).Count() / (double)n;
            score.DistanceAccuracy = Assign(distance).Where((j, i) => distance[i, j] == 0).Count() / (double)n;
            score.Precision = correct / (double)m;
            score.DirectionError = totalError / m;
            score.FalseResponseRate = 1 - score.Precision.Value;

            // An alternative correct-pair set must omit at least one of the chosen correct pairs.
            for (int i = 0; i < m && !score.Ambiguous; i++)
            {
                if (!score.JointCorrect[i]) continue;
                int j = match[i];
                double cost = joint[i, j];
                joint[i, j] = penalty * (m + 1.0);
                int[] alternative = Assign(joint);
                score.Ambiguous = alternative[i] != j &&
                    alternative.Where((column, row) => joint[row, column] < penalty).Count() == correct;
                joint[i, j] = cost;
            }
            return score;
        }

        // Rectangular Hungarian assignment. Rows are responses and columns are original threats.
        static int[] Assign(double[,] cost)
        {
            int rows = cost.GetLength(0), cols = cost.GetLength(1);
            var rowPotential = new double[rows + 1];
            var colPotential = new double[cols + 1];
            var owner = new int[cols + 1];
            var previous = new int[cols + 1];
            for (int row = 1; row <= rows; row++)
            {
                owner[0] = row;
                int current = 0;
                var slack = Enumerable.Repeat(double.PositiveInfinity, cols + 1).ToArray();
                var visited = new bool[cols + 1];
                do
                {
                    visited[current] = true;
                    int next = 0;
                    double delta = double.PositiveInfinity;
                    for (int col = 1; col <= cols; col++)
                    {
                        if (visited[col]) continue;
                        double reduced = cost[owner[current] - 1, col - 1] - rowPotential[owner[current]] - colPotential[col];
                        if (reduced < slack[col])
                        {
                            slack[col] = reduced;
                            previous[col] = current;
                        }
                        if (slack[col] < delta)
                        {
                            delta = slack[col];
                            next = col;
                        }
                    }
                    for (int col = 0; col <= cols; col++)
                    {
                        if (visited[col])
                        {
                            rowPotential[owner[col]] += delta;
                            colPotential[col] -= delta;
                        }
                        else slack[col] -= delta;
                    }
                    current = next;
                } while (owner[current] != 0);
                do
                {
                    int before = previous[current];
                    owner[current] = owner[before];
                    current = before;
                } while (current != 0);
            }
            var match = new int[rows];
            for (int col = 1; col <= cols; col++)
                if (owner[col] != 0) match[owner[col] - 1] = col - 1;
            return match;
        }
    }
}
