using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;


namespace Harmonics
{
    public class PlanktonNormals : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlanktonNormals class.
        /// </summary>
        public PlanktonNormals()
          : base("PlanktonNormals", "Normals",
              "Calculate the normalised vertex normals of an arbitrary mesh topology as the weighted average of the adjacent face normals",
              "Harmonics", "0 Mesh")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PlanktonMesh", "pMesh", "PlanktonMesh to use topology from", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("VertexNormals", "n", "The vertex normals", GH_ParamAccess.list);
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

            //calculate vertex normals
            List<Vector3d> vertexNormals = new List<Vector3d>();

            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                Vector3d vNormal = calcWeightedFacesNormal(pMesh, i);
                vertexNormals.Add(vNormal);
            }

            //output
            DA.SetDataList(0, vertexNormals);
        }

        //--------------------------------------------------Calculate vertex normals methods------------------------------------------//
        /*The vertex normals are calculated as the weighted average of the adjacent face normals */

        //FACE NORMAL
        /* calculate the face normal (not normalised) as average of cross products of edge pairs (n-gon) */
        public Vector3d calcFaceNormal(PlanktonMesh pMesh, int faceIndex)
        {
            Vector3d[] edgesCCW = new Vector3d[pMesh.Vertices.Count];

            int[] faceHalfedges = pMesh.Faces.GetHalfedges(faceIndex);
            for (int i = 0; i < faceHalfedges.Length; i++)
            {
                int startVertex = pMesh.Halfedges[faceHalfedges[i]].StartVertex;
                Point3d start = new Point3d(pMesh.Vertices[startVertex].X, pMesh.Vertices[startVertex].Y, pMesh.Vertices[startVertex].Z);
                int nextHalfedge = pMesh.Halfedges[faceHalfedges[i]].NextHalfedge;
                int endVertex = pMesh.Halfedges[nextHalfedge].StartVertex;
                Point3d end = new Point3d(pMesh.Vertices[endVertex].X, pMesh.Vertices[endVertex].Y, pMesh.Vertices[endVertex].Z);

                edgesCCW[i] = new Vector3d(end - start);
            }

            //shift edgesCCW
            Vector3d[] shift_edgesCCW = new Vector3d[edgesCCW.Length];
            for (int j = 0; j < edgesCCW.Length; j++)
            {
                shift_edgesCCW[j] = edgesCCW[(j + 1) % edgesCCW.Length];
            }

            //normal vector
            Vector3d normal = new Vector3d(0, 0, 0);
            for (int k = 0; k < edgesCCW.Length; k++)
            {
                normal += Vector3d.CrossProduct(edgesCCW[k], shift_edgesCCW[k]);
            }
            normal = normal / edgesCCW.Length;

            return normal;
        }


        //WEIGHTED FACE NORMALS
        /* calculate vertex normals as weighted average of the adjacent face normals */

        public Vector3d calcWeightedFacesNormal(PlanktonMesh pMesh, int i)
        {
            int[] adjFaces = pMesh.Vertices.GetVertexFaces(i);
            Vector3d vNormal = new Vector3d(0, 0, 0);
            foreach (int face in adjFaces)
            {
                if (face != -1)
                {
                    vNormal += calcFaceNormal(pMesh, face);
                }
            }
            vNormal.Unitize();
            return vNormal;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.VertexNormalsIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("63a92eb2-8575-4351-89bf-a05e297959f4"); }
        }
    }
}