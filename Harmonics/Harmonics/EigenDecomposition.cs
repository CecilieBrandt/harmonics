using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper;
using System.Numerics;

namespace Harmonics
{
    public class EigenDecomposition : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the EigenDecomposition class.
        /// </summary>
        public EigenDecomposition()
          : base("EigenDecomposition", "EigenDecomp",
              "Calculate the eigenvalues/eigenvectors of a matrix",
              "Harmonics", "1 Matrix")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMatrixParameter("LaplacianMatrix", "L", "The Laplacian matrix (symmetric) for the eigenvalue problem", GH_ParamAccess.item);
            pManager.AddIntegerParameter("EigenCount", "n", "The number of eigenvalues/vectors to extract. By default all eigenvalues/vectors are output", GH_ParamAccess.item);
            pManager.AddBooleanParameter("RunCalculation", "run", "If true, run calculation", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("eigenValues", "lambda", "The calculated eigenvalues as a list", GH_ParamAccess.list);
            pManager.AddMatrixParameter("eigenvectorMatrix", "v", "The calculated eigenvectors in matrix form (columns)", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Input
            Matrix mL = null;
            DA.GetData(0, ref mL);

            //size of n x n matrix
            int n = mL.RowCount;


            //Number of eigenvalues/vectors to extract
            int eigsCount = n;

            if (Params.Input[1].SourceCount>0)
            {
                DA.GetData(1, ref eigsCount);
            }

            if(eigsCount > n)
            {
                eigsCount = n;
            }
            else if(eigsCount < 1)
            {
                eigsCount = 1;
            }


            //Run calculation toggle
            bool run = false;
            DA.GetData(2, ref run);


            //Calculate

            //Create lists to store data
            List<double> eigenValuesOutput = new List<double>();
            Matrix eigenVectorMatrixOutput = null;


            if (run && mL!=null)
            {
                //Create matrix for MathNet eigendecomposition
                var matrix = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(n, n);

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrix[i, j] = mL[i, j];
                    }
                }

                //Eigendecomposition
                var evd = matrix.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);

                MathNet.Numerics.LinearAlgebra.Vector<Complex> eigsValComplex = evd.EigenValues;
                MathNet.Numerics.LinearAlgebra.Vector<Double> eigsValReal = eigsValComplex.Map(c => c.Real);
                MathNet.Numerics.LinearAlgebra.Matrix<double> eigsVec = evd.EigenVectors;


                //Convert data back to GH types

                //Eigenvalue list
                for(int i=0; i< eigsCount; i++)
                {
                    eigenValuesOutput.Add(eigsValReal[i]);
                }

                //Eigenvectors
                eigenVectorMatrixOutput = new Matrix(n, eigsCount);
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < eigsCount; j++)
                    {
                        double val = eigsVec[i, j];
                        eigenVectorMatrixOutput[i, j] = val;
                    }
                }

            }


            //Output
            DA.SetDataList(0, eigenValuesOutput);
            DA.SetData(1, eigenVectorMatrixOutput);       
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.EigenvaluesIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("db82208f-bb34-49be-9891-c4cf7c5e6734"); }
        }
    }
}