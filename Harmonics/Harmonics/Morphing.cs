using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;

namespace Harmonics
{
    public class Morphing : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Morphing class.
        /// </summary>
        public Morphing()
          : base("Morphing", "Morph",
              "Morph one mesh into another mesh generated from the same footprint following a non-linear path",
              "Harmonics", "2 Harmonics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Footprint", "footprint", "The footprint (planktonMesh) to use topology from", GH_ParamAccess.item);
            pManager.AddVectorParameter("VertexNormals", "n", "The vertex normals", GH_ParamAccess.list);
            pManager.AddIntegerParameter("EigenvectorIndices1", "vI1", "The eigenvector indices for shape 1", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights1", "w1", "The weights for shape 1", GH_ParamAccess.list);
            pManager.AddNumberParameter("Scalefactor1", "s1", "The scale factor for shape 1", GH_ParamAccess.item);
            pManager.AddIntegerParameter("EigenvectorIndices2", "vI2", "The eigenvector indices for shape 2", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights2", "w2", "The weights for mesh 2", GH_ParamAccess.list);
            pManager.AddNumberParameter("Scalefactor2", "s2", "The scale factor for mesh 2", GH_ParamAccess.item);
            pManager.AddMatrixParameter("SharedEigenvectorMatrix", "v", "The full shared eigenvector matrix", GH_ParamAccess.item);
            pManager.AddNumberParameter("MorphCoefficient", "coeff", "The morph coefficients in the range between 0.0 and 1.0 for each mode. The last coefficient corresponds to the scalefactor", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("MorphedPlanktonMesh", "morph", "The morphed planktonMesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("EigenvectorIndices", "vIndices", "The eigenvector indices used for the transition between shape 1 and shape 2", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Interval", "I", "The interval of the weights corresponding to the eigenvectors", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = null;
            DA.GetData(0, ref pMesh);

            List<Vector3d> directions = new List<Vector3d>();
            DA.GetDataList(1, directions);

            List<int> vIndices1 = new List<int>();
            DA.GetDataList(2, vIndices1);

            List<double> weights1 = new List<double>();
            DA.GetDataList(3, weights1);

            double scalefactor1 = 1.0;
            DA.GetData(4, ref scalefactor1);

            List<int> vIndices2 = new List<int>();
            DA.GetDataList(5, vIndices2);

            List<double> weights2 = new List<double>();
            DA.GetDataList(6, weights2);

            double scalefactor2 = 1.0;
            DA.GetData(7, ref scalefactor2);

            Matrix mV = null;
            DA.GetData(8, ref mV);

            List<double> lambda = new List<double>();
            DA.GetDataList(9, lambda);


            //------------------------------------------------------------------

            //Create list of modes without duplicates
            List<int> vIndices12 = createTotalModeList(vIndices1, vIndices2);

            //Create list of weights corresponding to this new list of modes
            List<double> adjWeights1 = new List<double>();
            List<double> adjWeights2 = new List<double>();

            foreach (int i in vIndices12)
            {
                //shape 1
                int index1 = vIndices1.IndexOf(i);
                if (index1 == -1)
                {
                    adjWeights1.Add(0.0);
                }
                else
                {
                    adjWeights1.Add(weights1[index1]);
                }

                //shape 2
                int index2 = vIndices2.IndexOf(i);
                if (index2 == -1)
                {
                    adjWeights2.Add(0.0);
                }
                else
                {
                    adjWeights2.Add(weights2[index2]);
                }
            }

            //Create intervals only for output purpose
            List<Interval> weightIntervals = new List<Interval>();
            for (int j = 0; j < vIndices12.Count; j++)
            {
                weightIntervals.Add(new Interval(adjWeights1[j], adjWeights2[j]));
            }


            //Lambda values test
            List<double> lambdaUpdate = lambdaTest(lambda, vIndices12.Count);


            //Morph vector
            List<double> morphVector = calcTotalMorphVector(mV, vIndices12, adjWeights1, adjWeights2, lambdaUpdate);

            //Scale factor
            double morphScale = calcScaleFactorInt(scalefactor1, scalefactor2, lambdaUpdate[lambdaUpdate.Count - 1]);

            //Displacement vectors
            List<Vector3d> displacements = mapToDisplacements(morphVector, directions, morphScale);

            //New mesh from displacements
            PlanktonMesh pMeshMorph = new PlanktonMesh(pMesh);
            for (int i = 0; i < pMeshMorph.Vertices.Count; i++)
            {
                pMeshMorph.Vertices[i].X += (float)displacements[i].X;
                pMeshMorph.Vertices[i].Y += (float)displacements[i].Y;
                pMeshMorph.Vertices[i].Z += (float)displacements[i].Z;
            }


            //------------------------------------------------------------------

            //Output
            DA.SetData(0, pMeshMorph);
            DA.SetDataList(1, vIndices12);
            DA.SetDataList(2, weightIntervals);
        }

        //Methods

        //Create list of modes without duplicates
        public List<int> createTotalModeList(List<int> vIndices1, List<int> vIndices2)
        {
            List<int> modes = new List<int>();
            foreach (int i in vIndices1)
            {
                modes.Add(i);
            }

            foreach (int j in vIndices2)
            {
                if (!modes.Contains(j))
                {
                    modes.Add(j);
                }
            }

            return modes;
        }


        //Test lambda values and size
        public List<double> lambdaTest(List<double> _lambda, int numberOfModes)
        {
            //Default lambda values
            List<double> lambdaDefault = new List<double>();

            for (int i = 0; i < numberOfModes + 1; i++)
            {
                lambdaDefault.Add(1.0);
            }

            //test
            bool accepted = true;

            //value range
            foreach (double d in _lambda)
            {
                if (d < 0.0 || d > 1.0)
                {
                    accepted = false;
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The morph coefficients are restricted to be within the range from 0.0 to 1.0. Coefficients set to 1.0 by default");
                }
            }

            //number of sliders
            if (_lambda.Count != numberOfModes + 1)
            {
                accepted = false;
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("The number of coefficients have to equal {0}. Coefficients set to 1.0 by default", numberOfModes + 1));
            }

            //return list based on test
            if (!accepted)
            {
                _lambda = lambdaDefault;
            }
            return _lambda;
        }


        //Calculate morph vector of one mode from formula
        public List<double> calcModeMorphVector(Matrix mV, int vIndex, double w1, double w2, double lambda)
        {
            List<double> vecA = new List<double>();
            List<double> vecB = new List<double>();

            for (int i = 0; i < mV.RowCount; i++)
            {
                vecA.Add(mV[i, vIndex] * w1);
                vecB.Add(mV[i, vIndex] * w2);
            }

            //Morph vector
            List<double> vecMorph = new List<double>();

            for (int j = 0; j < mV.RowCount; j++)
            {
                double val = (1.0 - lambda) * vecA[j] + lambda * vecB[j];
                vecMorph.Add(val);
            }

            return vecMorph;
        }

        //Calculate the non-linear morph sum vector
        public List<double> calcTotalMorphVector(Matrix mV, List<int> vIndices, List<double> w1, List<double> w2, List<double> lambda)
        {
            List<double> vecTotalMorph = new List<double>();
            for (int i = 0; i < mV.RowCount; i++)
            {
                vecTotalMorph.Add(0.0);
            }

            //eigsIndices.Count tells how many modes are combined (the last number is related to the scale and not included here)
            for (int i = 0; i < vIndices.Count; i++)
            {
                List<double> vecModeMorph = calcModeMorphVector(mV, vIndices[i], w1[i], w2[i], lambda[i]);

                //sum up
                for (int j = 0; j < mV.RowCount; j++)
                {
                    vecTotalMorph[j] += vecModeMorph[j];
                }
            }

            return vecTotalMorph;
        }


        //Scale factor
        public double calcScaleFactorInt(double s1, double s2, double lambda)
        {
            double scale = (1 - lambda) * s1 + lambda * s2;
            return scale;
        }


        //Map to displacement
        public List<Vector3d> mapToDisplacements(List<double> nodalValues, List<Vector3d> displDir, double scale)
        {
            List<Vector3d> nodalDisplacements = new List<Vector3d>();

            for (int i = 0; i < nodalValues.Count; i++)
            {
                Vector3d dir = displDir[i];
                dir.Unitize();
                nodalDisplacements.Add((dir * (nodalValues[i] * scale)));
            }
            return nodalDisplacements;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.MorphingIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("73a9fe53-c4af-40b7-b93e-14a9203cb68e"); }
        }
    }
}