using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Plankton;

namespace Harmonics
{
    public class CotangentLaplacian : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CotangentLaplacian class.
        /// </summary>
        public CotangentLaplacian()
          : base("CotangentLaplacian", "CotLap",
              "Calculate the Laplacian with cotangent weightings",
              "Harmonics", "1 Matrix")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("PlanktonMesh", "PMesh", "PlanktonMesh to use topology from (has to be triangulated)", GH_ParamAccess.item);
            pManager.AddIntegerParameter("AreaWeightingOption", "Opt", "0: Barycenter.  1: Voronoi.  2: Unweighted", GH_ParamAccess.item, 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMatrixParameter("CotangentLaplacian", "L", "The Laplacian matrix with cotangent weightings", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Global variables
            PlanktonMesh pMesh = null;
            DA.GetData<PlanktonMesh>(0, ref pMesh);

            int opt = 1;
            DA.GetData(1, ref opt);

            if (opt > 2)
            {
                opt = 2;
            }
            else if (opt < 0)
            {
                opt = 0;
            }

            //-------------------------------------------

            //Test if triangular input mesh
            bool triangular = isTriangular(pMesh);
            if (!triangular)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The input mesh has to be triangulated");
            }

            //A list of areas parallel to vertices
            List<double> vertexAreas = vertexAreaList(pMesh, opt);

            //A list of cotangent weights parallel to halfedges
            List<double> cotEdgeWeights = cotEdgeWeightList(pMesh, vertexAreas, opt);

            //The cotangent laplacian matrix
            Matrix mL = calcCotLaplacian(pMesh, cotEdgeWeights);

            //-------------------------------------------


            //Output
            DA.SetData(0, mL);
        }

        //Methods

        //TRIANGULAR TEST
        /* test if it is a triangular input mesh */
        public bool isTriangular(PlanktonMesh pMesh)
        {
            bool triangular = false;

            int faceCount = pMesh.Faces.Count;
            int triCount = 0;

            for (int i = 0; i < faceCount; i++)
            {
                int[] faceVertices = pMesh.Faces.GetFaceVertices(i);
                if (faceVertices.Length == 3)
                {
                    triCount++;
                }
            }

            if (faceCount == triCount)
            {
                triangular = true;
            }

            return triangular;
        }


        //EDGE VECTOR
        /* return the edge vector from a halfedge index pointing from halfedge start towards halfedge end */
        public Vector3d edgeVector(PlanktonMesh pMesh, int halfedgeIndex)
        {
            PlanktonHalfedge pHalfedge = pMesh.Halfedges[halfedgeIndex];

            //start vertex
            PlanktonVertex pVStart = pMesh.Vertices[pHalfedge.StartVertex];
            Point3d startVertex = new Point3d(pVStart.X, pVStart.Y, pVStart.Z);

            //end vertex
            PlanktonVertex pVEnd = pMesh.Vertices[pMesh.Halfedges[pHalfedge.NextHalfedge].StartVertex];
            Point3d endVertex = new Point3d(pVEnd.X, pVEnd.Y, pVEnd.Z);

            //edge vector
            Vector3d vecEdge = new Vector3d(endVertex - startVertex);

            return vecEdge;
        }


        /* return the edge vector from vertex i to vertex j */
        public Vector3d edgeVector(PlanktonMesh pMesh, int vertex_i, int vertex_j)
        {
            Point3d pt_i = new Point3d(pMesh.Vertices[vertex_i].X, pMesh.Vertices[vertex_i].Y, pMesh.Vertices[vertex_i].Z);
            Point3d pt_j = new Point3d(pMesh.Vertices[vertex_j].X, pMesh.Vertices[vertex_j].Y, pMesh.Vertices[vertex_j].Z);

            Vector3d vecEdge = pt_j - pt_i;

            return vecEdge;
        }


        /* return the outgoing edge vector when standing in a specific vertex in a face */
        public Vector3d edgeVectorOut(PlanktonMesh pMesh, int vertexIndex, int faceIndex)
        {
            int[] faceHalfedges = pMesh.Faces.GetHalfedges(faceIndex);

            int halfedgeV = -1;
            foreach (int index in faceHalfedges)
            {
                if (pMesh.Halfedges[index].StartVertex == vertexIndex)
                {
                    halfedgeV = index;
                    break;
                }
            }

            Vector3d vecEdge = new Vector3d();
            if (halfedgeV != -1)
            {
                vecEdge = edgeVector(pMesh, halfedgeV);
            }

            return vecEdge;
        }


        /* return the incoming edge vector when standing in a specific vertex in a face */
        public Vector3d edgeVectorIn(PlanktonMesh pMesh, int vertexIndex, int faceIndex)
        {
            int[] faceHalfedges = pMesh.Faces.GetHalfedges(faceIndex);

            int halfedgeV = -1;
            foreach (int index in faceHalfedges)
            {
                if (pMesh.Halfedges[index].StartVertex == vertexIndex)
                {
                    halfedgeV = pMesh.Halfedges[index].PrevHalfedge;
                    break;
                }
            }

            Vector3d vecEdge = new Vector3d();
            if (halfedgeV != -1)
            {
                vecEdge = edgeVector(pMesh, halfedgeV);
            }

            return vecEdge;
        }



        //AREA
        /* calculate area of a triangle as magnitude of cross product divided by 2 */
        public double calcAreaTriangle(PlanktonMesh pMesh, int faceIndex)
        {
            int[] faceVertices = pMesh.Faces.GetFaceVertices(faceIndex);

            Vector3d vecEdge0 = edgeVectorOut(pMesh, faceVertices[0], faceIndex);
            Vector3d vecEdge1 = edgeVectorOut(pMesh, faceVertices[1], faceIndex);

            Vector3d vecArea = Vector3d.CrossProduct(vecEdge0, vecEdge1);
            double area = vecArea.Length / 2;

            return area;
        }


        //VERTEX ANGLE
        /* calculate the angle at a specific vertex in a face (n-gon) */
        public double calcVertexAngle(PlanktonMesh pMesh, int faceIndex, int vertexIndex)
        {
            Vector3d vertexEdgeOut = edgeVectorOut(pMesh, vertexIndex, faceIndex);
            Vector3d vertexEdgeIn = edgeVectorIn(pMesh, vertexIndex, faceIndex);
            vertexEdgeIn.Reverse();

            double angle = 0.0;
            if (vertexEdgeOut.Length != 0.0 && vertexEdgeIn.Length != 0.0)
            {
                angle = Vector3d.VectorAngle(vertexEdgeOut, vertexEdgeIn);
            }

            return angle;
        }



        //OBTUSE TEST
        /* test if there exists an obtuse/right angle in a given face (n-gon) */
        public bool isObtuse(PlanktonMesh pMesh, int faceIndex)
        {
            bool obtuse = false;

            int[] faceVertices = pMesh.Faces.GetFaceVertices(faceIndex);
            foreach (int i in faceVertices)
            {
                double angle = calcVertexAngle(pMesh, faceIndex, i);
                if (angle >= Math.PI / 2)
                {
                    obtuse = true;
                }
            }
            return obtuse;
        }



        //ANGLE OPPOSITE HALFEDGE
        /* calculate the angle opposite a halfedge assuming triangular mesh. 
        The angle is set to 0.0 if the halfedge is located at the boundary (i.e. missing triangle) */

        public double calcOppositeAngle(PlanktonMesh pMesh, int halfedgeIndex)
        {
            double angle = 0.0;
            int faceIndex = pMesh.Halfedges[halfedgeIndex].AdjacentFace;

            if (faceIndex != -1)
            {
                //find previous halfedge
                int prevHalfedge = pMesh.Halfedges[halfedgeIndex].PrevHalfedge;
                int vertexIndexOpposite = pMesh.Halfedges[prevHalfedge].StartVertex;

                angle = calcVertexAngle(pMesh, faceIndex, vertexIndexOpposite);
            }

            return angle;
        }


        //VERTEX INDICES FROM HALFEDGE INDEX
        /* return vertex index i and j given a halfedge index */
        public int[] findHalfedgeVertexIndices(PlanktonMesh pMesh, int halfedgeIndex)
        {
            int[] indices = new int[2];
            indices[0] = pMesh.Halfedges[halfedgeIndex].StartVertex;
            indices[1] = pMesh.Halfedges[pMesh.Halfedges[halfedgeIndex].NextHalfedge].StartVertex;

            return indices;
        }


        //HALFEDGE INDEX FROM VERTEX INDICES
        /* return halfedge index from given i and j vertex indices. -1 if halfedge doesn't exist */
        public int findHalfedgeIndex(PlanktonMesh pMesh, int i, int j)
        {
            //find halfedge from vertex i to j if it exists
            int halfedge_ij = pMesh.Halfedges.FindHalfedge(i, j);

            return halfedge_ij;
        }


        //COTANGENT EDGE WEIGHT
        /* calculate cotangent weight for edge with vertex indices (i,j). 
        The weightings must ensure a symmetric matrix where the rows and columns sum to zero */

        public double calcCotEdgeWeight(PlanktonMesh pMesh, int i, int j, List<double> vertexAreas, int opt)
        {
            double w_ij;

            //find halfedge from vertex i to j if it exists
            int halfedge_ij = findHalfedgeIndex(pMesh, i, j);
            if (halfedge_ij == -1)
            {
                w_ij = 0.0;
            }
            else
            {
                int halfedge_ji = pMesh.Halfedges.GetPairHalfedge(halfedge_ij);

                double alfa = calcOppositeAngle(pMesh, halfedge_ij);
                double beta = calcOppositeAngle(pMesh, halfedge_ji);

                if (alfa == 0.0)
                {
                    w_ij = 1 / Math.Tan(beta);
                }
                else if (beta == 0.0)
                {
                    w_ij = 1 / Math.Tan(alfa);
                }
                else
                {
                    w_ij = 1 / Math.Tan(alfa) + 1 / Math.Tan(beta);
                }
            }

            //Area option

            //barycentric or voronoi
            if (opt == 0 || opt == 1)
            {
                double area_i = vertexAreas[i];
                double area_j = vertexAreas[j];
                w_ij /= Math.Sqrt(area_i * area_j);
            }

            return w_ij;
        }



        //COTANGENT EDGE WEIGHTS LIST
        /* calculate the cotangent edge weights ordered in a list parallel to halfedges */
        public List<double> cotEdgeWeightList(PlanktonMesh pMesh, List<double> vertexAreas, int opt)
        {
            List<double> cotWeights = new List<double>();

            for (int i = 0; i < pMesh.Halfedges.Count / 2; i++)
            {
                int[] indices = findHalfedgeVertexIndices(pMesh, (i * 2));
                double val = calcCotEdgeWeight(pMesh, indices[0], indices[1], vertexAreas, opt);
                //add two times corresponding to pair halfedge
                cotWeights.Add(val);
                cotWeights.Add(val);
            }
            return cotWeights;
        }


        //COTANGENT VERTEX WEIGHT
        /* calculate cotangent weight for vertex i from neighbourhood of edges */

        public double calcCotVertexWeight(PlanktonMesh pMesh, int vertexIndex, List<double> cotEdgeWeights)
        {
            int[] vertexNeighbours = pMesh.Vertices.GetVertexNeighbours(vertexIndex);

            double vertexWeightSum = 0.0;
            foreach (int vertexNeighbour in vertexNeighbours)
            {
                int halfedgeIndex = findHalfedgeIndex(pMesh, vertexIndex, vertexNeighbour);
                vertexWeightSum += cotEdgeWeights[halfedgeIndex];
            }
            return vertexWeightSum;
        }



        //AREA CALCULATION OF NON-OVERLAPPING TILES
        /* calculate the barycentric area (1/3 T) */
        public double calcVertexBarycentricArea(PlanktonMesh pMesh, int vertexIndex)
        {

            double areaB = 0.0;

            //adjacent triangular faces
            int[] vertexFaces = pMesh.Vertices.GetVertexFaces(vertexIndex);

            foreach (int faceIndex in vertexFaces)
            {
                if (faceIndex != -1)
                {
                    double areaT = calcAreaTriangle(pMesh, faceIndex);
                    areaB += areaT / 3;
                }
            }
            return areaB;
        }


        /* calculate the voronoi area (cotangent or 1/2 or 1/4 T). Hybrid approach which handles obtuse triangles as well */
        public double calcVertexVoronoiArea(PlanktonMesh pMesh, int vertexIndex)
        {
            double areaV = 0.0;

            //adjacent triangular faces
            int[] vertexFaces = pMesh.Vertices.GetVertexFaces(vertexIndex);

            foreach (int faceIndex in vertexFaces)
            {
                if (faceIndex != -1)
                {
                    double areaT = calcAreaTriangle(pMesh, faceIndex);

                    //if obtuse angle exists in triangle
                    if (isObtuse(pMesh, faceIndex))
                    {
                        //if v is the location of the obtuse angle
                        if (calcVertexAngle(pMesh, faceIndex, vertexIndex) >= Math.PI / 2)
                        {
                            areaV += areaT / 2;
                        }
                        else
                        {
                            areaV += areaT / 4;
                        }
                    }
                    //if non-obtuse
                    else
                    {
                        int[] faceHalfedges = pMesh.Faces.GetHalfedges(faceIndex);

                        int halfedgeOut = -1;
                        foreach (int index in faceHalfedges)
                        {
                            if (pMesh.Halfedges[index].StartVertex == vertexIndex)
                            {
                                halfedgeOut = index;
                                break;
                            }
                        }

                        int halfedgeIn = pMesh.Halfedges[halfedgeOut].PrevHalfedge;

                        areaV += (1 / 8.0) * ((Math.Pow(edgeVector(pMesh, halfedgeOut).Length, 2) * (1 / Math.Tan(calcOppositeAngle(pMesh, halfedgeOut))) + Math.Pow(edgeVector(pMesh, halfedgeIn).Length, 2) * (1 / Math.Tan(calcOppositeAngle(pMesh, halfedgeIn)))));
                    }
                }
            }
            return areaV;
        }


        //AREA LIST
        /*calculate the area corresponding to each vertex (depending on option) and append to a list parallel to vertices */
        public List<double> vertexAreaList(PlanktonMesh pMesh, int opt)
        {
            List<double> vAreas = new List<double>();
            for (int i = 0; i < pMesh.Vertices.Count; i++)
            {
                if (opt == 0)
                {
                    vAreas.Add(calcVertexBarycentricArea(pMesh, i));
                }
                else if (opt == 1)
                {
                    vAreas.Add(calcVertexVoronoiArea(pMesh, i));
                }
                else if (opt == 2)
                {
                    vAreas.Add(0.0);
                }
            }


            //Map areas to 0-10 domain so it is scale-independent of geometry
            List<double> vAreasMapped = new List<double>();

            if (opt == 0 || opt == 1)
            {
                double areaMax = vAreas.Max();

                foreach (double area in vAreas)
                {
                    double area_map = (area / areaMax) * 10.0;
                    vAreasMapped.Add(area_map);
                }
            }
            else if (opt == 2)
            {
                foreach (double area in vAreas)
                {
                    vAreasMapped.Add(area);
                }
            }

            return vAreasMapped;

        }


        //COTANGENT LAPLACIAN
        /* calculate the cotangent Laplacian of the input mesh */
        public Matrix calcCotLaplacian(PlanktonMesh pMesh, List<double> cotEdgeWeights)
        {
            int n = pMesh.Vertices.Count;
            Matrix mL = new Matrix(n, n);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        mL[i, j] = calcCotVertexWeight(pMesh, i, cotEdgeWeights);
                    }
                    else
                    {
                        int edgeIndex = findHalfedgeIndex(pMesh, i, j);
                        if (edgeIndex != -1)
                        {
                            mL[i, j] = -1 * cotEdgeWeights[edgeIndex];
                        }
                    }
                }
            }
            return mL;
        }


        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                return Properties.Resources.CotangentLaplacianIcon;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("d4ac9056-7030-4f75-98e7-fc31d4fd53ae"); }
        }
    }
}