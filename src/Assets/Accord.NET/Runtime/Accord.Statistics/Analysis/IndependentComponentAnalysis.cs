﻿// Accord Statistics Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2014
// cesarsouza at gmail.com
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace Accord.Statistics.Analysis
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Accord.Math;
    using Accord.Math.Decompositions;
    using Accord.Statistics.Analysis.ContrastFunctions;

    /// <summary>
    ///   FastICA's algorithms to be used in Independent Component Analysis.
    /// </summary>
    /// 
    public enum IndependentComponentAlgorithm
    {
        /// <summary>
        ///   Deflation algorithm.
        /// </summary>
        /// <remarks>
        ///   In the deflation algorithm, components are found one
        ///   at a time through a series of sequential operations.
        ///   It is particularly useful when only a small number of
        ///   components should be computed from the input data set.
        /// </remarks>
        /// 
        Deflation,

        /// <summary>
        ///   Symmetric parallel algorithm (default).
        /// </summary>
        /// <remarks>
        ///   In the parallel (symmetric) algorithm, all components
        ///   are computed at once. This is the default algorithm for
        ///   <seealso cref="IndependentComponentAnalysis">Independent
        ///   Component Analysis</seealso>.
        /// </remarks>
        /// 
        Parallel,
    }

    /// <summary>
    ///   Independent Component Analysis (ICA).
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    ///   Independent Component Analysis is a computational method for separating
    ///   a multivariate signal (or mixture) into its additive subcomponents, supposing
    ///   the mutual statistical independence of the non-Gaussian source signals.</para>
    /// <para>
    ///   When the independence assumption is correct, blind ICA separation of a mixed
    ///   signal gives very good results. It is also used for signals that are not supposed
    ///   to be generated by a mixing for analysis purposes.</para>  
    /// <para>
    ///   A simple application of ICA is the "cocktail party problem", where the underlying
    ///   speech signals are separated from a sample data consisting of people talking
    ///   simultaneously in a room. Usually the problem is simplified by assuming no time
    ///   delays or echoes.</para>
    /// <para>
    ///   An important note to consider is that if N sources are present, at least N
    ///   observations (e.g. microphones) are needed to get the original signals.</para>
    ///   
    /// <para>
    ///   References:
    ///   <list type="bullet">
    ///     <item><description>
    ///       Hyvärinen, A (1999). Fast and Robust Fixed-Point Algorithms for Independent Component
    ///       Analysis. IEEE Transactions on Neural Networks, 10(3),626-634. Available on: 
    ///       <a href="http://citeseer.ist.psu.edu/viewdoc/summary?doi=10.1.1.50.4731">
    ///       http://citeseer.ist.psu.edu/viewdoc/summary?doi=10.1.1.50.4731 </a></description></item>
    ///     <item><description>
    ///       E. Bingham and A. Hyvärinen A fast fixed-point algorithm for independent component
    ///       analysis of complex-valued signals. Int. J. of Neural Systems, 10(1):1-8, 2000. </description></item>
    ///     <item><description>
    ///       FastICA: FastICA Algorithms to perform ICA and Projection Pursuit. Available on:
    ///       <a href="http://cran.r-project.org/web/packages/fastICA/index.html">
    ///       http://cran.r-project.org/web/packages/fastICA/index.html </a></description></item>
    ///     <item><description>
    ///       Wikipedia, The Free Encyclopedia. Independent component analysis. Available on:
    ///       http://en.wikipedia.org/wiki/Independent_component_analysis </description></item>
    ///  </list></para>  
    /// </remarks>
    /// 
    /// <example>
    /// <code>
    /// // Let's create a random dataset containing
    /// // 5000 samples of two dimensional samples.
    /// //
    /// double[,] source = Matrix.Random(5000, 2);
    /// 
    /// // Now, we will mix the samples the dimensions of the samples.
    /// // A small amount of the second column will be applied to the
    /// // first, and vice-versa. 
    /// //
    /// double[,] mix =
    /// {
    ///     {  0.25, 0.25 },
    ///     { -0.25, 0.75 },    
    /// };
    /// 
    /// // mix the source data
    /// double[,] input = source.Multiply(mix);
    /// 
    /// // Now, we can use ICA to identify any linear mixing between the variables, such
    /// // as the matrix multiplication we did above. After it has identified it, we will
    /// // be able to revert the process, retrieving our original samples again
    ///             
    /// // Create a new Independent Component Analysis
    /// var ica = new IndependentComponentAnalysis(input);
    /// 
    /// 
    /// // Compute it 
    /// ica.Compute();
    /// 
    /// // Now, we can retrieve the mixing and demixing matrices that were 
    /// // used to alter the data. Note that the analysis was able to detect
    /// // this information automatically:
    /// 
    /// double[,] mixingMatrix = ica.MixingMatrix; // same as the 'mix' matrix
    /// double[,] revertMatrix = ica.DemixingMatrix; // inverse of the 'mix' matrix
    /// </code>
    /// </example>
    /// 
    [Serializable]
    public class IndependentComponentAnalysis : IMultivariateAnalysis
    {
        private double[,] sourceMatrix;


        private double[,] whiteningMatrix; // pre-whitening matrix
        private double[,] mixingMatrix; // estimated mixing matrix
        private double[,] revertMatrix; // estimated de-mixing matrix
        private double[,] resultMatrix;

        private Single[][] revertArray; // for caching conversions
        private Single[][] mixingArray;

        private AnalysisMethod analysisMethod = AnalysisMethod.Center;
        private bool overwriteSourceMatrix;

        private double[] columnMeans;
        private double[] columnStdDev;

        private int maxIterations = 100;
        private double tolerance = 1e-3;

        private IndependentComponentAlgorithm algorithm;
        private IContrastFunction contrast = new Logcosh();

        private IndependentComponentCollection componentCollection;


        //---------------------------------------------


        #region Constructors
        /// <summary>
        ///   Constructs a new Independent Component Analysis.
        /// </summary>
        /// 
        /// <param name="data">The source data to perform analysis. The matrix should contain
        ///   variables as columns and observations of each variable as rows.</param>
        /// 
        public IndependentComponentAnalysis(double[,] data)
            : this(data, AnalysisMethod.Center, IndependentComponentAlgorithm.Parallel)
        {
        }

        /// <summary>
        ///   Constructs a new Independent Component Analysis.
        /// </summary>
        /// 
        /// <param name="data">The source data to perform analysis. The matrix should contain
        ///   variables as columns and observations of each variable as rows.</param>
        /// <param name="algorithm">The FastICA algorithm to be used in the analysis. Default
        ///   is <see cref="IndependentComponentAlgorithm.Parallel"/>.</param>
        ///   
        public IndependentComponentAnalysis(double[,] data, IndependentComponentAlgorithm algorithm)
            : this(data, AnalysisMethod.Center, algorithm)
        {
        }

        /// <summary>
        ///   Constructs a new Independent Component Analysis.
        /// </summary>
        /// 
        /// <param name="data">The source data to perform analysis. The matrix should contain
        ///   variables as columns and observations of each variable as rows.</param>
        /// <param name="method">The analysis method to perform. Default is
        ///   <see cref="AnalysisMethod.Center"/>.</param>
        /// 
        public IndependentComponentAnalysis(double[,] data, AnalysisMethod method)
            : this(data, method, IndependentComponentAlgorithm.Parallel)
        {
        }

        /// <summary>
        ///   Constructs a new Independent Component Analysis.
        /// </summary>
        /// 
        /// <param name="data">The source data to perform analysis. The matrix should contain
        ///   variables as columns and observations of each variable as rows.</param>
        /// <param name="method">The analysis method to perform. Default is
        ///   <see cref="AnalysisMethod.Center"/>.</param>
        /// <param name="algorithm">The FastICA algorithm to be used in the analysis. Default
        ///   is <see cref="IndependentComponentAlgorithm.Parallel"/>.</param>
        ///   
        public IndependentComponentAnalysis(double[,] data, AnalysisMethod method,
            IndependentComponentAlgorithm algorithm)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            this.sourceMatrix = data;
            this.algorithm = algorithm;
            this.analysisMethod = method;

            // Calculate common measures to speedup other calculations
            this.columnMeans = Accord.Statistics.Tools.Mean(sourceMatrix);
            this.columnStdDev = Accord.Statistics.Tools.StandardDeviation(sourceMatrix, columnMeans);
        }

        /// <summary>
        ///   Constructs a new Independent Component Analysis.
        /// </summary>
        /// 
        /// <param name="data">The source data to perform analysis. The matrix should contain
        ///   variables as columns and observations of each variable as rows.</param>
        /// <param name="method">The analysis method to perform. Default is
        ///   <see cref="AnalysisMethod.Center"/>.</param>
        /// <param name="algorithm">The FastICA algorithm to be used in the analysis. Default
        ///   is <see cref="IndependentComponentAlgorithm.Parallel"/>.</param>
        ///   
        public IndependentComponentAnalysis(double[][] data, AnalysisMethod method = AnalysisMethod.Center,
          IndependentComponentAlgorithm algorithm = IndependentComponentAlgorithm.Parallel)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            this.sourceMatrix = data.ToMatrix();
            this.algorithm = algorithm;
            this.analysisMethod = method;

            // Calculate common measures to speedup other calculations
            this.columnMeans = Accord.Statistics.Tools.Mean(sourceMatrix);
            this.columnStdDev = Accord.Statistics.Tools.StandardDeviation(sourceMatrix, columnMeans);
        }

        #endregion


        //---------------------------------------------


        #region Properties
        /// <summary>
        ///   Source data used in the analysis.
        /// </summary>
        /// 
        public double[,] Source
        {
            get { return sourceMatrix; }
        }

        /// <summary>
        ///   Gets or sets the maximum number of iterations 
        ///   to perform. If zero, the method will run until
        ///   convergence.
        /// </summary>
        /// 
        /// <value>The iterations.</value>
        /// 
        public int Iterations
        {
            get { return maxIterations; }
            set { maxIterations = value; }
        }

        /// <summary>
        ///   Gets or sets the maximum absolute change in
        ///   parameters between iterations that determine
        ///   convergence.
        /// </summary>
        /// 
        public double Tolerance
        {
            get { return tolerance; }
            set { tolerance = value; }
        }

        /// <summary>
        ///   Gets the resulting projection of the source
        ///   data given on the creation of the analysis 
        ///   into the space spawned by independent components.
        /// </summary>
        /// 
        /// <value>The resulting projection in independent component space.</value>
        /// 
        public double[,] Result
        {
            get { return resultMatrix; }
        }

        /// <summary>
        ///   Gets a matrix containing the mixing coefficients for
        ///   the original source data being analyzed. Each column
        ///   corresponds to an independent component.
        /// </summary>
        /// 
        public double[,] MixingMatrix
        {
            get { return mixingMatrix; }
        }

        /// <summary>
        ///   Gets a matrix containing the demixing coefficients for
        ///   the original source data being analyzed. Each column
        ///   corresponds to an independent component.
        /// </summary>
        /// 
        public double[,] DemixingMatrix
        {
            get { return revertMatrix; }
        }

        /// <summary>
        ///   Gets the whitening matrix used to transform
        ///   the original data to have unit variance.
        /// </summary>
        /// 
        public double[,] WhiteningMatrix
        {
            get { return whiteningMatrix; }
        }

        /// <summary>
        ///   Gets the Independent Components in a object-oriented structure.
        /// </summary>
        /// 
        /// <value>The collection of independent components.</value>
        /// 
        public IndependentComponentCollection Components
        {
            get { return componentCollection; }
        }

        /// <summary>
        ///   Gets or sets whether calculations will be performed overwriting
        ///   data in the original source matrix, using less memory.
        /// </summary>
        /// 
        public bool Overwrite
        {
            get { return overwriteSourceMatrix; }
            set { overwriteSourceMatrix = value; }
        }

        /// <summary>
        ///  Gets or sets the <see cref="IndependentComponentAlgorithm">
        ///  FastICA</see> algorithm used by the analysis.
        /// </summary>
        /// 
        public IndependentComponentAlgorithm Algorithm
        {
            get { return algorithm; }
            set { algorithm = value; }
        }

        /// <summary>
        ///   Gets or sets the <see cref="IContrastFunction">
        ///   Contrast function</see> to be used by the analysis.
        /// </summary>
        /// 
        public IContrastFunction Contrast
        {
            get { return contrast; }
            set { contrast = value; }
        }

        /// <summary>
        ///   Gets the column means of the original data.
        /// </summary>
        /// 
        public double[] Means
        {
            get { return columnMeans; }
        }

        /// <summary>
        ///   Gets the column standard deviations of the original data.
        /// </summary>
        /// 
        public double[] StandardDeviation
        {
            get { return columnStdDev; }
        }
        #endregion



        //---------------------------------------------


        #region Public methods

        /// <summary>
        ///   Computes the Independent Component Analysis algorithm.
        /// </summary>
        /// 
        public void Compute()
        {
            Compute(sourceMatrix.GetLength(1));
        }

        /// <summary>
        ///   Computes the Independent Component Analysis algorithm.
        /// </summary>
        /// 
        public void Compute(int components)
        {
            // First, the data should be centered by subtracting
            //  the mean of each column in the source data matrix.
            double[,] matrix = Adjust(sourceMatrix, overwriteSourceMatrix);

            // Pre-process the centered data matrix to have unit variance
            double[,] whiten = Statistics.Tools.Whitening(matrix, out whiteningMatrix);

            // Generate a new unitary initial guess for the de-mixing matrix
            double[,] initial = Matrix.Random(components, matrix.GetLength(1), 0, 1);


            // Compute the demixing matrix using the selected algorithm
            if (algorithm == IndependentComponentAlgorithm.Deflation)
            {
                revertMatrix = deflation(whiten, components, initial);
            }
            else // if (algorithm == IndependentComponentAlgorithm.Parallel)
            {
                revertMatrix = parallel(whiten, components, initial);
            }

            

            // Combine the rotation and demixing matrices
            revertMatrix = whiteningMatrix.MultiplyByTranspose(revertMatrix);
            normalize(revertMatrix);

            // Compute the original source mixing matrix
            mixingMatrix = Matrix.PseudoInverse(revertMatrix);
            normalize(mixingMatrix);

            // Demix the data into independent components
            resultMatrix = matrix.Multiply(revertMatrix);

            
            // Creates the object-oriented structure to hold the principal components
            var array = new IndependentComponent[components];
            for (int i = 0; i < array.Length; i++)
                array[i] = new IndependentComponent(this, i);
            this.componentCollection = new IndependentComponentCollection(array);
        }

        private static void normalize(double[,] matrix)
        {
            double sum = 0;
            foreach (double v in matrix)
                sum += v;
            matrix.Divide(sum, inPlace: true);
        }

        /// <summary>
        ///   Separates a mixture into its components (demixing).
        /// </summary>
        /// 
        public double[,] Separate(double[,] data)
        {
            // Data-adjust and separate the samples
            double[,] matrix = Adjust(data, false);

            return matrix.Multiply(revertMatrix);
        }

        /// <summary>
        ///   Separates a mixture into its components (demixing).
        /// </summary>
        /// 
        public float[][] Separate(float[][] data)
        {
            if (revertArray == null)
            {
                // Convert reverting matrix to single
                revertArray = convertToSingle(revertMatrix);
            }

            // Data-adjust and separate the sources
            float[][] matrix = Adjust(data, false);

            return revertArray.Multiply(matrix);
        }

        /// <summary>
        ///   Separates a mixture into its components (demixing).
        /// </summary>
        /// 
        public double[][] Separate(double[][] data)
        {
            if (revertArray == null)
            {
                // Convert reverting matrix to single
                revertArray = convertToSingle(revertMatrix);
            }

            // Data-adjust and separate the sources
            double[][] matrix = Adjust(data, false);

            return revertArray.Multiply(matrix);
        }


        /// <summary>
        ///   Combines components into a single mixture (mixing).
        /// </summary>
        /// 
        public double[,] Combine(double[,] data)
        {
            return data.Multiply(mixingMatrix);
        }

        /// <summary>
        ///   Combines components into a single mixture (mixing).
        /// </summary>
        /// 
        public float[][] Combine(float[][] data)
        {
            if (mixingArray == null)
            {
                // Convert mixing matrix to single
                mixingArray = convertToSingle(mixingMatrix);
            }

            return mixingArray.Multiply(data);
        }
        #endregion


        //---------------------------------------------


        #region FastICA Algorithms

        /// <summary>
        ///   Deflation iterative algorithm.
        /// </summary>
        /// 
        /// <returns>
        ///   Returns a matrix in which each row contains
        ///   the mixing coefficients for each component.
        /// </returns>
        /// 
        private double[,] deflation(double[,] X, int components, double[,] init)
        {
            // References:
            // - Hyvärinen, A (1999). Fast and Robust Fixed-Point
            //   Algorithms for Independent Component Analysis.

            // There are two ways to apply the fixed-unit algorithm to compute the whole
            // ICA iteration. The more simpler is to perform a deflation as in the Gram-
            // Schmidt orthogonalization process [Hyvärinen]. In this scheme, independent
            // components are estimated one-by-one. See referenced paper for details.

            int n = X.GetLength(0);
            int m = X.GetLength(1);

            // Algorithm initialization
            double[,] W = new double[components, m];
            double[] wx = new double[n];
            double[] gwx = new double[n];
            double[] dgwx = new double[n];


            // For each component to be computed,
            for (int i = 0; i < components; i++)
            {
                // Will compute each of the basis vectors
                //  individually and sequentially, re-using
                //  previous computations to form basis W. 
                //  

                // Initialization
                int iterations = 0;
                bool stop = false;
                double lastChange = 1;

                double[] w = init.GetRow(i);
                double[] w0 = init.GetRow(i);


                do // until convergence
                {
                    // Start with deflation
                    for (int u = 0; u < i; u++)
                    {
                        double proj = 0;
                        for (int j = 0; j < w0.Length; j++)
                            proj += w0[j] * W[u, j];

                        for (int j = 0; j < w0.Length; j++)
                            w[j] = w0[j] - proj * W[u, j];
                    }

                    // Normalize
                    w = w.Divide(Norm.Euclidean(w));


                    // Gets the maximum parameter absolute change
                    double delta = getMaximumAbsoluteChange(w, w0);

                    // Check for convergence
                    if (!(delta > tolerance * lastChange && iterations < maxIterations))
                    {
                        stop = true;
                    }
                    else
                    {
                        // Advance to the next iteration
                        w0 = w; w = new double[m];
                        lastChange = delta;
                        iterations++;

                        // Compute wx = w*x
                        for (int j = 0; j < n; j++)
                        {
                            double s = 0;
                            for (int k = 0; k < w0.Length; k++)
                                s += w0[k] * X[j, k];
                            wx[j] = s;
                        }

                        // Compute g(w*x) and g'(w*x)
                        contrast.Evaluate(wx, gwx, dgwx);

                        // Compute E{ x*g(w*x) }
                        double[] means = new double[m];
                        for (int j = 0; j < means.Length; j++)
                        {
                            for (int k = 0; k < gwx.Length; k++)
                                means[j] += X[k, j] * gwx[k];
                            means[j] /= n;
                        }

                        // Compute E{ g'(w*x) }
                        double mean = Statistics.Tools.Mean(dgwx);


                        // Compute next update for w according
                        //  to Hyvärinen paper's equation (20).

                        // w+ = E{ xg(w*x)} - E{ g'(w*x)}*w
                        for (int j = 0; j < means.Length; j++)
                            w[j] = means[j] - mean * w0[j];

                        // The normalization to w* will be performed
                        //  in the beginning of the next iteration.
                    }

                } while (!stop);

                // Store the just computed component
                // in the resulting component matrix.
                W.SetRow(i, w);
            }

            // Return the component basis matrix
            return W; // vectors stored as rows.
        }


        /// <summary>
        ///   Parallel (symmetric) iterative algorithm.
        /// </summary>
        /// 
        /// <returns>
        ///   Returns a matrix in which each row contains
        ///   the mixing coefficients for each component.
        /// </returns>
        /// 
        private double[,] parallel(double[,] X, int components, double[,] winit)
        {
            // References:
            // - Hyvärinen, A (1999). Fast and Robust Fixed-Point
            //   Algorithms for Independent Component Analysis.

            // There are two ways to apply the fixed-unit algorithm to compute the whole
            // ICA iteration. The second approach is to perform orthogonalization at once
            // using an Eigendecomposition [Hyvärinen]. The Eigendecomposition can in turn
            // be converted to a more stable singular value decomposition and be used to 
            // create a projection basis in the same way as in Principal Component Analysis.

            int n = X.GetLength(0);
            int m = X.GetLength(1);

            // Algorithm initialization
            double[,] W0 = winit;
            double[,] W = winit;
            double[,] K = new double[components, components];

            bool stop = false;
            int iterations = 0;
            double lastChange = 1;


            do // until convergence
            {

                // [Hyvärinen, 1997]'s paper suggests the use of the Eigendecomposition
                //   to orthogonalize W (after equation 10). However, [E, D] = eig(W'W)
                //   can be replaced by [U, S] = svd(W), which is more stable and avoids
                //   computing W'W. Since the singular values are already the square roots
                //   of the eigenvalues of W'W, the computation of E'*D^(-1/2)*E' reduces
                //   to U*S^(-1)*U'. 

                // Perform simultaneous decorrelation of all components at once
                var svd = new SingularValueDecomposition(W,
                    computeLeftSingularVectors: true,
                    computeRightSingularVectors: false,
                    autoTranspose: true);

                double[] S = svd.Diagonal;
                double[,] U = svd.LeftSingularVectors;

                // Form orthogonal projection basis K
                for (int i = 0; i < components; i++)
                {
                    for (int j = 0; j < components; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < S.Length; k++)
                            if (S[k] != 0.0)
                                s += U[i, k] * U[j, k] / S[k];
                        K[i, j] = s;
                    }
                }

                // Orthogonalize
                W = K.Multiply(W);


                // Gets the maximum parameter absolute change
                double delta = getMaximumAbsoluteChange(W0, W);

                // Check for convergence
                if (delta < tolerance * lastChange || iterations >= maxIterations)
                {
                    stop = true;
                }
                else
                {
                    // Advance to the next iteration
                    W0 = W; W = new double[components, m];
                    lastChange = delta;
                    iterations++;


                    // For each component (in parallel)
                    global::Accord.Threading.Tasks.Parallel.For(0, components, i =>
                    {
                        double[] wx = new double[n];
                        double[] dgwx = new double[n];
                        double[] gwx = new double[n];
                        double[] means = new double[m];

                        // Compute wx = w*x
                        for (int j = 0; j < wx.Length; j++)
                        {
                            double s = 0;
                            for (int k = 0; k < m; k++)
                                s += W0[i, k] * X[j, k];
                            wx[j] = s;
                        }

                        // Compute g(wx) and g'(wx)
                        contrast.Evaluate(wx, gwx, dgwx);

                        // Compute E{ x*g(w*x) }
                        for (int j = 0; j < means.Length; j++)
                        {
                            for (int k = 0; k < gwx.Length; k++)
                                means[j] += X[k, j] * gwx[k];
                            means[j] /= n;
                        }

                        // Compute E{ g'(w*x) }
                        double mean = Statistics.Tools.Mean(dgwx);


                        // Compute next update for w according
                        //  to Hyvärinen paper's equation (20).

                        // w+ = E{ xg(w*x)} - E{ g'(w*x)}*w
                        for (int j = 0; j < means.Length; j++)
                            W[i, j] = means[j] - mean * W0[i, j];

                        // The normalization to w* will be performed
                        //  in the beginning of the next iteration.
                    });
                }

            } while (!stop);

            // Return the component basis matrix
            return W; // vectors stored as rows.
        }


        #endregion


        //---------------------------------------------


        #region Auxiliary methods

        /// <summary>
        ///   Adjusts a data matrix, centering and standardizing its values
        ///   using the already computed column's means and standard deviations.
        /// </summary>
        /// 
        protected double[,] Adjust(double[,] matrix, bool inPlace)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            // Prepare the data, storing it in a new matrix if needed.
            double[,] result = inPlace ? matrix : new double[rows, cols];


            // Center the data around the mean. Will have no effect if
            //  the data is already centered (the mean will be zero).
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i, j] = (matrix[i, j] - columnMeans[j]);

            // Check if we also have to standardize our data (convert to Z Scores).
            if (this.analysisMethod == AnalysisMethod.Standardize)
            {
                // Yes. Divide by standard deviation
                for (int j = 0; j < cols; j++)
                {
                    if (columnStdDev[j] == 0)
                        throw new ArithmeticException("Standard deviation cannot be zero (cannot standardize the constant variable at column index " + j + ").");

                    for (int i = 0; i < rows; i++)
                        result[i, j] /= columnStdDev[j];
                }
            }

            return result;
        }

        /// <summary>
        ///   Adjusts a data matrix, centering and standardizing its values
        ///   using the already computed column's means and standard deviations.
        /// </summary>
        /// 
        protected float[][] Adjust(float[][] matrix, bool inPlace)
        {
            int rows = matrix.Length;
            int cols = matrix[0].Length;

            // Prepare the data, storing it in a new matrix if needed.
            float[][] result = matrix;

            if (!inPlace)
            {
                result = new float[rows][];
                for (int i = 0; i < rows; i++)
                    result[i] = new float[cols];
            }

            // Center the data around the mean. Will have no effect if
            //  the data is already centered (the mean will be zero).
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i][j] = (matrix[i][j] - (float)columnMeans[i]);

            // Check if we also have to standardize our data (convert to Z Scores).
            if (this.analysisMethod == AnalysisMethod.Standardize)
            {
                // Yes. Divide by standard deviation
                for (int i = 0; i < rows; i++)
                {
                    if (columnStdDev[i] == 0)
                        throw new ArithmeticException("Standard deviation cannot be zero (cannot standardize the constant variable at column index " + i + ").");

                    for (int j = 0; j < rows; j++)
                        result[i][j] /= (float)columnStdDev[i];
                }
            }

            return result;
        }

        /// <summary>
        ///   Adjusts a data matrix, centering and standardizing its values
        ///   using the already computed column's means and standard deviations.
        /// </summary>
        /// 
        protected double[][] Adjust(double[][] matrix, bool inPlace)
        {
            int rows = matrix.Length;
            int cols = matrix[0].Length;

            // Prepare the data, storing it in a new matrix if needed.
            double[][] result = matrix;

            if (!inPlace)
            {
                result = new double[rows][];
                for (int i = 0; i < rows; i++)
                    result[i] = new double[cols];
            }


            // Center the data around the mean. Will have no effect if
            //  the data is already centered (the mean will be zero).
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i][j] = (matrix[i][j] - columnMeans[i]);

            // Check if we also have to standardize our data (convert to Z Scores).
            if (this.analysisMethod == AnalysisMethod.Standardize)
            {
                // Yes. Divide by standard deviation
                for (int i = 0; i < rows; i++)
                {
                    if (columnStdDev[i] == 0)
                        throw new ArithmeticException("Standard deviation cannot be zero (cannot standardize the constant variable at column index " + i + ").");

                    for (int j = 0; j < rows; j++)
                        result[i][j] /= columnStdDev[i];
                }
            }

            return result;
        }

        private static Single[][] convertToSingle(double[,] matrix)
        {
            int components = matrix.GetLength(0);
            float[][] array = new float[components][];
            for (int i = 0; i < components; i++)
            {
                array[i] = new float[components];
                for (int j = 0; j < components; j++)
                    array[i][j] = (float)matrix[j, i];
            }

            return array;
        }

        #endregion

        /// <summary>
        ///   Computes the maximum absolute change between two members of a matrix.
        /// </summary>
        /// 
        private static double getMaximumAbsoluteChange(double[,] W, double[,] W0)
        {
            int rows = W0.GetLength(0);
            int cols = W0.GetLength(1);

            double max = Math.Abs(W0[0, 0] - W[0, 0]);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double d = Math.Abs(W0[i, j] - W[i, j]);
                    if (d > max) max = d;
                }
            }

            return max;
        }

        /// <summary>
        ///   Computes the maximum absolute change between two members of a vector.
        /// </summary>
        /// 
        private static double getMaximumAbsoluteChange(double[] w, double[] w0)
        {
            double max = Math.Abs(w[0] - w0[0]);
            for (int i = 1; i < w.Length; i++)
            {
                double d = Math.Abs(w[i] - w0[i]);
                if (d > max) max = d;
            }

            return max;
        }
    }


    #region Support Classes

    /// <summary>
    ///   Represents an Independent Component found in the Independent Component 
    ///   Analysis, allowing it to be directly bound to controls like the DataGridView.
    /// </summary>
    /// 
    [Serializable]
    public class IndependentComponent
    {

        private int index;
        private IndependentComponentAnalysis analysis;


        /// <summary>
        ///   Creates an independent component representation.
        /// </summary>
        /// 
        /// <param name="analysis">The analysis to which this component belongs.</param>
        /// <param name="index">The component index.</param>
        /// 
        internal IndependentComponent(IndependentComponentAnalysis analysis, int index)
        {
            this.index = index;
            this.analysis = analysis;
        }


        /// <summary>
        ///   Gets the Index of this component on the original component collection.
        /// </summary>
        /// 
        public int Index
        {
            get { return this.index; }
        }

        /// <summary>
        ///   Returns a reference to the parent analysis object.
        /// </summary>
        /// 
        public IndependentComponentAnalysis Analysis
        {
            get { return this.analysis; }
        }

        /// <summary>
        ///   Gets the mixing vector for the current independent component.
        /// </summary>
        /// 
        public double[] MixingVector
        {
            get { return this.analysis.MixingMatrix.GetColumn(index); }
        }

        /// <summary>
        ///   Gets the demixing vector for the current independent component.
        /// </summary>
        /// 
        public double[] DemixingVector
        {
            get { return this.analysis.DemixingMatrix.GetColumn(index); }
        }

        /// <summary>
        ///   Gets the whitening factor for the current independent component.
        /// </summary>
        /// 
        public double[] WhiteningVector
        {
            get { return this.analysis.WhiteningMatrix.GetColumn(index); }
        }

    }

    /// <summary>
    ///   Represents a Collection of Independent Components found in the
    ///   Independent Component Analysis. This class cannot be instantiated.
    /// </summary>
    /// 
    [Serializable]
    public class IndependentComponentCollection : ReadOnlyCollection<IndependentComponent>
    {
        internal IndependentComponentCollection(IndependentComponent[] components)
            : base(components)
        {
        }
    }
    #endregion


}
