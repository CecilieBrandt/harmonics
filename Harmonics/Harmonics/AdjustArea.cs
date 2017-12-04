using System;
using System.Collections.Generic;
using Plankton;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;

namespace Harmonics
{
    public class AdjustArea : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the AdjustArea class.
        /// </summary>
        public AdjustArea()
          : base("AdjustArea", "AdjustArea",
              "Adjust the weights to match a specific percentage in area increase. Only works for triangulated meshes",
              "Harmonics", "4 Utility")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("pMeshInit", "pMeshInit", "The initial Plankton Mesh before the target approximation", GH_ParamAccess.item);
            pManager.AddMatrixParameter("EigenvectorMatrix", "v", "The eigenvector matrix", GH_ParamAccess.item);
            pManager.AddVectorParameter("VertexNormals", "n", "The vertex normals", GH_ParamAccess.list);
            pManager.AddNumberParameter("TargetWeights", "tw", "The weights of the target", GH_ParamAccess.list);
            pManager.AddNumberParameter("TargetScale", "ts", "The scale factor of the target", GH_ParamAccess.item);
            pManager.AddNumberParameter("NewWeights", "nw", "The new weights", GH_ParamAccess.list);
            pManager.AddNumberParameter("NewScale", "ns", "The new scale factor", GH_ParamAccess.item);
            pManager.AddNumberParameter("Percentage", "p", "The desired area increase in percent", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("AreaConsistentPMesh", "pMeshA", "The area consistent Plankton Mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Area", "area", "Area of the rescaled mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("AdjustedWeights", "aw", "The adjusted weights", GH_ParamAccess.list);
            pManager.AddNumberParameter("AdjustedScale", "as", "The adjusted scale factor", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMeshInit = new PlanktonMesh();
            DA.GetData(0, ref pMeshInit);
            if (!isTriangulated(pMeshInit))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The mesh has to be triangulated");
            }

            Matrix mV = null;
            DA.GetData(1, ref mV);

            List<Vector3d> vertexNormals = new List<Vector3d>();
            DA.GetDataList(2, vertexNormals);

            List<double> targetWeights = new List<double>();
            DA.GetDataList(3, targetWeights);

            double targetScale = 1.0;
            DA.GetData(4, ref targetScale);

            List<double> newWeights = new List<double>();
            DA.GetDataList(5, newWeights);

            double newScale = 1.0;
            DA.GetData(6, ref newScale);

            double p = 1.0;
            DA.GetData(7, ref p);
            p = Math.Round(p, 1);

            //--------------------------------------------------------------------------------

            //MESHES
            //Reference target mesh and its area
            PlanktonMesh pMeshTarget = createPlankton(pMeshInit, mV, vertexNormals, targetWeights, targetScale);
            double areaTarget = calcPMeshArea(pMeshTarget);

            //New mesh and its area
            PlanktonMesh pMeshNew = createPlankton(pMeshInit, mV, vertexNormals, newWeights, newScale);
            double areaNew = calcPMeshArea(pMeshNew);


            //Calculate the current area deviation in percent
            double areaDiff = Math.Round(((areaNew - areaTarget) * 100.0) / areaTarget);



            //BISECTION

            //Initialise
            double factor = 0.0;            //specifies e.g. 10% of travel distance (positive if the area has to increase)
            double stepSize = 0.1;
            int reverse = 1;

            if (areaDiff > p)
            {
                stepSize *= -1;
                reverse = -1;
            }


            //Current state
            List<double> weightsAdjusted = new List<double>();
            foreach (double d in newWeights)
            {
                weightsAdjusted.Add(d);
            }
            double scaleAdjusted = newScale;
            PlanktonMesh pMeshAdjusted = new PlanktonMesh(pMeshNew);
            double areaAdjusted = areaNew;


            //Iteration
            int iter = 0;
            while (areaDiff != p && iter < 100)
            {
                //Adjust travel distance factor
                factor += stepSize;

                //Adjust the weights as a linear interpolation
                weightsAdjusted = calcAdjustedWeights(targetWeights, newWeights, factor);
                scaleAdjusted = calcAdjustedScale(targetScale, newScale, factor);

                //Create adjusted Plankton mesh
                pMeshAdjusted = createPlankton(pMeshInit, mV, vertexNormals, weightsAdjusted, scaleAdjusted);
                areaAdjusted = calcPMeshArea(pMeshAdjusted);
                areaDiff = Math.Round(((areaAdjusted - areaTarget) * 100.0) / areaTarget, 1);

                //Refine stepsize if the iteration overshoots the area increase percentage
                if (areaDiff * reverse > p * reverse)
                {
                    factor -= stepSize;
                    stepSize /= 2;
                }

                //Maximum number of iterations criteria
                iter++;
                if (iter == 100)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Maximum number of iterations has been reached");
                }
            }


            //--------------------------------------------------------------------------------

            //Output
            //In case the weight has exceeded (+)(-) 1.0
            double scaleAdjustedN = calcScaleFactor(weightsAdjusted);

            if (scaleAdjustedN > 1.0)
            {
                List<double> weightsAdjustedN = calcNormalisedWeights(weightsAdjusted, scaleAdjustedN, false);
                for (int k = 0; k < weightsAdjusted.Count; k++)
                {
                    weightsAdjusted[k] = weightsAdjustedN[k];
                }

                scaleAdjusted *= scaleAdjustedN;
            }


            DA.SetData(0, pMeshAdjusted);
            DA.SetData(1, areaAdjusted);
            DA.SetDataList(2, weightsAdjusted);
            DA.SetData(3, scaleAdjusted);
        }

        //Methods

        //IsMeshTriangulated
        public bool isTriangulated(PlanktonMesh pMesh)
        {
            bool isTriangle = true;

            for (int i = 0; i < pMesh.Faces.Count(); i++)
            {
                int[] faceVertices = pMesh.Faces.GetFaceVertices(i);
                if (faceVertices.Length != 3)
                {
                    isTriangle = false;
                }
            }
            return isTriangle;
        }

        //Linear combination of eigenvectors
        public double[] nodalLinearCombination(List<double> weights, Matrix mV)
        {
            //A value per node
            double[] nodalValues = new double[mV.RowCount];

            for (int i = 0; i < mV.RowCount; i++)
            {
                double nValue = 0.0;
                for (int j = 0; j < mV.ColumnCount; j++)
                {
                    nValue += weights[j] * mV[i, j];
                }
                nodalValues[i] = nValue;
            }
            return nodalValues;
        }

        //Map to displacement
        public Vector3d[] mapToDisplacements(double[] nodalValues, List<Vector3d> displDir, double scale)
        {
            Vector3d[] nodalDisplacements = new Vector3d[nodalValues.Length];

            for (int i = 0; i < nodalValues.Length; i++)
            {
                Vector3d dir = displDir[i];
                dir.Unitize();
                nodalDisplacements[i] = dir * (nodalValues[i] * scale);
            }
            return nodalDisplacements;
        }


        //Create new plankton mesh
        public PlanktonMesh createPlankton(PlanktonMesh pMeshInit, Matrix mV, List<Vector3d> normals, List<double> weights, double scale)
        {
            PlanktonMesh pMeshNew = new PlanktonMesh(pMeshInit);

            double[] nodalValues = nodalLinearCombination(weights, mV);
            Vector3d[] displacements = mapToDisplacements(nodalValues, normals, scale);

            for (int i = 0; i < pMeshNew.Vertices.Count; i++)
            {
                pMeshNew.Vertices[i].X += (float)displacements[i].X;
                pMeshNew.Vertices[i].Y += (float)displacements[i].Y;
                pMeshNew.Vertices[i].Z += (float)displacements[i].Z;
            }

            return pMeshNew;
        }


        //Calculate face area
        public double calcFaceArea(PlanktonMesh pMesh, int index)
        {
            int[] faceVertices = pMesh.Faces.GetFaceVertices(index);

            List<Vector3d> vecFaceVertices = new List<Vector3d>();
            foreach (int i in faceVertices)
            {
                Vector3d vec = new Vector3d(pMesh.Vertices[i].X, pMesh.Vertices[i].Y, pMesh.Vertices[i].Z);
                vecFaceVertices.Add(vec);
            }

            Vector3d vecEdge1 = Vector3d.Subtract(vecFaceVertices[1], vecFaceVertices[0]);
            Vector3d vecEdge2 = Vector3d.Subtract(vecFaceVertices[2], vecFaceVertices[0]);

            Vector3d cross = Vector3d.CrossProduct(vecEdge1, vecEdge2);

            double faceArea = cross.Length / 2.0;

            return faceArea;
        }


        //Calculate pMesh area
        public double calcPMeshArea(PlanktonMesh pMesh)
        {
            double area = 0.0;

            for (int i = 0; i < pMesh.Faces.Count; i++)
            {
                area += calcFaceArea(pMesh, i);
            }

            return area;
        }


        //Calculate the scale factor from the list of weights
        public double calcScaleFactor(List<double> weights)
        {
            double wMin = weights.Min();
            double wMax = weights.Max();

            double scale = wMax;
            if (Math.Abs(wMin) > wMax)
            {
                scale = Math.Abs(wMin);
            }

            return scale;
        }


        //Calculate normalised weights such that the largest value is +-1.0
        public List<double> calcNormalisedWeights(List<double> weights, double scale, bool round)
        {
            List<double> normalisedWeights = new List<double>();
            foreach (double w in weights)
            {
                double val = w;
                if (scale != 0.0)
                {
                    val /= scale;
                }

                if (round)
                {
                    val = Math.Round(val, 2);
                }

                normalisedWeights.Add(val);
            }
            return normalisedWeights;
        }

        //Calculate the distance between the target and new weights. Returns a number between -1.0 and 1.0
        public List<double> calcDeltaWeights(List<double> targetWeights, List<double> newWeights)
        {
            List<double> deltaWeights = new List<double>();

            for (int i = 0; i < targetWeights.Count(); i++)
            {
                deltaWeights.Add(Math.Abs(targetWeights[i] - newWeights[i]));
            }

            return deltaWeights;
        }

        //Calculate the adjusted weights
        public List<double> calcAdjustedWeights(List<double> targetWeights, List<double> newWeights, double factor)
        {
            List<double> adjustedWeights = new List<double>();
            List<double> deltaWeights = calcDeltaWeights(targetWeights, newWeights);

            for (int i = 0; i < deltaWeights.Count(); i++)
            {
                double dist = deltaWeights[i] * factor;
                double val = newWeights[i] + dist;
                if (newWeights[i] < 0)
                {
                    val = newWeights[i] - dist;
                }
                adjustedWeights.Add(val);
            }

            return adjustedWeights;
        }

        //Calculate the adjusted scale
        public double calcAdjustedScale(double targetScale, double newScale, double factor)
        {
            double deltaScale = Math.Abs(targetScale - newScale);
            double dist = deltaScale * factor;
            double adjustedScale = newScale + dist;

            return adjustedScale;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.AdjustedAreaIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("e5d89bd1-fb90-430a-b12e-e2e9fc008625"); }
        }
    }
}