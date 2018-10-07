using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Special;
using PlanktonGh;
using System.Drawing;

namespace Harmonics
{
    public class Eigenfunction : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the Eigenfunction class.
        /// </summary>
        public Eigenfunction()
          : base("Eigenfunction", "Eigenfunction",
              "Create harmonic shapes from a linear combination of the eigenvectors",
              "Harmonics", "2 Harmonics")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PMesh", "PMesh", "The input PlanktonMesh to use topology from", GH_ParamAccess.item);
            pManager.AddMatrixParameter("EigenvectorsMatrix", "v", "The eigenvectors matrix", GH_ParamAccess.item);
            pManager.AddVectorParameter("VertexNormals", "n", "The vertex normals to specify the displacement directions", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weights", "w", "A list of weights used for the linear combination", GH_ParamAccess.list);
            pManager.AddNumberParameter("ScaleFactor", "scale", "The scale factor of the displacements", GH_ParamAccess.item, 1.0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "The harmonic mesh (colour sprayed)", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = new PlanktonMesh();
            DA.GetData<PlanktonMesh>(0, ref pMesh);

            Matrix mV = null;
            DA.GetData(1, ref mV);
            int vCount = mV.ColumnCount;

            List<Vector3d> displDirections = new List<Vector3d>();
            DA.GetDataList(2, displDirections);

            List<double> weights = new List<double>();
            DA.GetDataList(3, weights);

            if (weights.Count != vCount)
            {
                weights = new List<double>();
                for (int i = 0; i < vCount; i++)
                {
                    weights.Add(1.0);
                }
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("The number of weights must equal {0}. Uniform weights are set as default", vCount));
            }

            double scale = 1.0;
            DA.GetData(4, ref scale);

            //----------------------------------------------------------------------------------------

            //Calculate nodal values from linear combination of eigenvectors
            double[] nodalValues = nodalLinearCombination(weights, mV);

            //Map nodal values to displacement vectors
            Vector3d[] displacements = mapToDisplacements(nodalValues, displDirections, scale);

            //Map nodal values to mesh vertex colours
            Color[] vertexColours = mapToColour(nodalValues);

            //New mesh from displacements
            PlanktonMesh pMeshNew = new PlanktonMesh(pMesh);
            for (int i = 0; i < pMeshNew.Vertices.Count; i++)
            {
                pMeshNew.Vertices[i].X += (float)displacements[i].X;
                pMeshNew.Vertices[i].Y += (float)displacements[i].Y;
                pMeshNew.Vertices[i].Z += (float)displacements[i].Z;
            }


            //Convert to Rhino Mesh and add vertex colouring if the same number of vertices exist
            Mesh rhinoMesh = pMeshNew.ToRhinoMesh();
            if (rhinoMesh.Vertices.Count == pMesh.Vertices.Count)
            {
                rhinoMesh.VertexColors.CreateMonotoneMesh(Color.White);

                for (int j = 0; j < rhinoMesh.VertexColors.Count; j++)
                {
                    rhinoMesh.VertexColors[j] = vertexColours[j];
                }
            }
            else
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh colouring failed due to a different number of vertices in the Rhino Mesh and PlanktonMesh. This happens if n-gons with n>4 exist");
            }

            //----------------------------------------------------------------------------------------

            //Output
            DA.SetData(0, rhinoMesh);
        }

        //Methods

        //Linear combination of eigenvectors
        public double[] nodalLinearCombination(List<double> weights, Matrix mV)
        {
            //A value pr. node
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

        //Map to colours
        public Color[] mapToColour(double[] nodalValues)
        {
            //list to contain vertex colours
            Color[] vertexColors = new Color[nodalValues.Length];

            //scale nodal values to make sure that rounding to integers doesn't result in zero values
            double[] nodalValuesScale = new double[nodalValues.Length];
            for (int k = 0; k < nodalValues.Length; k++)
            {
                nodalValuesScale[k] = nodalValues[k] * 1000;
            }


            //default colour black
            int t_color = 0;

            //add to colour array
            for (int j = 0; j < nodalValues.Length; j++)
            {
                vertexColors[j] = Color.FromArgb(t_color, t_color, t_color);
            }


            //check domain range of nodal values
            double domainRange = nodalValuesScale.Max() - nodalValuesScale.Min();

            //map value from nodalvalue domain to 0 - 255 domain if not constant values
            if (Convert.ToInt32(domainRange) != 0)
            {
                for (int i = 0; i < nodalValues.Length; i++)
                {
                    double t_normal = (nodalValuesScale[i] - nodalValuesScale.Min()) / (domainRange);
                    double t_map = 0 + t_normal * (255 - 0);
                    t_color = Convert.ToInt32(t_map);

                    vertexColors[i] = Color.FromArgb(t_color, t_color, t_color);
                }
            }
            return vertexColors;
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


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.EigenfunctionIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("a9e86dad-6a75-4edd-b555-aea0dca5676d"); }
        }
    }
}