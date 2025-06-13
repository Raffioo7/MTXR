using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MeshCutter : MonoBehaviour
{
    public struct CutResult
    {
        public Mesh positiveMesh;
        public Mesh negativeMesh;
    }
    
    public static CutResult CutMesh(Mesh mesh, Plane plane)
    {
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var normals = mesh.normals;
        var uvs = mesh.uv;
        
        var positiveVertices = new List<Vector3>();
        var negativeVertices = new List<Vector3>();
        var positiveTriangles = new List<int>();
        var negativeTriangles = new List<int>();
        var positiveNormals = new List<Vector3>();
        var negativeNormals = new List<Vector3>();
        var positiveUVs = new List<Vector2>();
        var negativeUVs = new List<Vector2>();
        
        // Dictionary to store intersection points to avoid duplicates
        var intersectionPoints = new Dictionary<string, int>();
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var v0 = vertices[triangles[i]];
            var v1 = vertices[triangles[i + 1]];
            var v2 = vertices[triangles[i + 2]];
            
            var n0 = normals[triangles[i]];
            var n1 = normals[triangles[i + 1]];
            var n2 = normals[triangles[i + 2]];
            
            var uv0 = uvs[triangles[i]];
            var uv1 = uvs[triangles[i + 1]];
            var uv2 = uvs[triangles[i + 2]];
            
            var d0 = plane.GetDistanceToPoint(v0);
            var d1 = plane.GetDistanceToPoint(v1);
            var d2 = plane.GetDistanceToPoint(v2);
            
            var signs = new bool[] { d0 >= 0, d1 >= 0, d2 >= 0 };
            var positiveCount = signs.Count(s => s);
            
            if (positiveCount == 3)
            {
                // Triangle is completely on positive side
                AddTriangle(positiveVertices, positiveTriangles, positiveNormals, positiveUVs,
                           v0, v1, v2, n0, n1, n2, uv0, uv1, uv2);
            }
            else if (positiveCount == 0)
            {
                // Triangle is completely on negative side
                AddTriangle(negativeVertices, negativeTriangles, negativeNormals, negativeUVs,
                           v0, v1, v2, n0, n1, n2, uv0, uv1, uv2);
            }
            else
            {
                // Triangle intersects the plane - need to split it
                SplitTriangle(v0, v1, v2, n0, n1, n2, uv0, uv1, uv2, d0, d1, d2, signs, plane,
                             positiveVertices, positiveTriangles, positiveNormals, positiveUVs,
                             negativeVertices, negativeTriangles, negativeNormals, negativeUVs);
            }
        }
        
        var result = new CutResult();
        result.positiveMesh = CreateMesh(positiveVertices, positiveTriangles, positiveNormals, positiveUVs);
        result.negativeMesh = CreateMesh(negativeVertices, negativeTriangles, negativeNormals, negativeUVs);
        
        return result;
    }
    
    private static void AddTriangle(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
                                   Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n0, Vector3 n1, Vector3 n2,
                                   Vector2 uv0, Vector2 uv1, Vector2 uv2)
    {
        int startIndex = vertices.Count;
        vertices.AddRange(new[] { v0, v1, v2 });
        normals.AddRange(new[] { n0, n1, n2 });
        uvs.AddRange(new[] { uv0, uv1, uv2 });
        triangles.AddRange(new[] { startIndex, startIndex + 1, startIndex + 2 });
    }
    
    private static void SplitTriangle(Vector3 v0, Vector3 v1, Vector3 v2,
                                     Vector3 n0, Vector3 n1, Vector3 n2,
                                     Vector2 uv0, Vector2 uv1, Vector2 uv2,
                                     float d0, float d1, float d2, bool[] signs, Plane plane,
                                     List<Vector3> posVertices, List<int> posTriangles, List<Vector3> posNormals, List<Vector2> posUVs,
                                     List<Vector3> negVertices, List<int> negTriangles, List<Vector3> negNormals, List<Vector2> negUVs)
    {
        // Find the vertex that's alone on one side
        int aloneIndex = -1;
        bool aloneSign = false;
        
        if (signs[0] != signs[1] && signs[0] != signs[2])
        {
            aloneIndex = 0;
            aloneSign = signs[0];
        }
        else if (signs[1] != signs[0] && signs[1] != signs[2])
        {
            aloneIndex = 1;
            aloneSign = signs[1];
        }
        else if (signs[2] != signs[0] && signs[2] != signs[1])
        {
            aloneIndex = 2;
            aloneSign = signs[2];
        }
        
        Vector3[] verts = { v0, v1, v2 };
        Vector3[] norms = { n0, n1, n2 };
        Vector2[] uvCoords = { uv0, uv1, uv2 };
        float[] distances = { d0, d1, d2 };
        
        if (aloneIndex != -1)
        {
            int next1 = (aloneIndex + 1) % 3;
            int next2 = (aloneIndex + 2) % 3;
            
            // Calculate intersection points
            float t1 = distances[aloneIndex] / (distances[aloneIndex] - distances[next1]);
            float t2 = distances[aloneIndex] / (distances[aloneIndex] - distances[next2]);
            
            Vector3 intersection1 = Vector3.Lerp(verts[aloneIndex], verts[next1], t1);
            Vector3 intersection2 = Vector3.Lerp(verts[aloneIndex], verts[next2], t2);
            
            Vector3 intersectionNorm1 = Vector3.Lerp(norms[aloneIndex], norms[next1], t1).normalized;
            Vector3 intersectionNorm2 = Vector3.Lerp(norms[aloneIndex], norms[next2], t2).normalized;
            
            Vector2 intersectionUV1 = Vector2.Lerp(uvCoords[aloneIndex], uvCoords[next1], t1);
            Vector2 intersectionUV2 = Vector2.Lerp(uvCoords[aloneIndex], uvCoords[next2], t2);
            
            if (aloneSign)
            {
                // Alone vertex goes to positive side
                AddTriangle(posVertices, posTriangles, posNormals, posUVs,
                           verts[aloneIndex], intersection1, intersection2,
                           norms[aloneIndex], intersectionNorm1, intersectionNorm2,
                           uvCoords[aloneIndex], intersectionUV1, intersectionUV2);
                
                // Other two vertices form quad on negative side
                AddTriangle(negVertices, negTriangles, negNormals, negUVs,
                           intersection1, verts[next1], verts[next2],
                           intersectionNorm1, norms[next1], norms[next2],
                           intersectionUV1, uvCoords[next1], uvCoords[next2]);
                
                AddTriangle(negVertices, negTriangles, negNormals, negUVs,
                           intersection1, verts[next2], intersection2,
                           intersectionNorm1, norms[next2], intersectionNorm2,
                           intersectionUV1, uvCoords[next2], intersectionUV2);
            }
            else
            {
                // Alone vertex goes to negative side
                AddTriangle(negVertices, negTriangles, negNormals, negUVs,
                           verts[aloneIndex], intersection1, intersection2,
                           norms[aloneIndex], intersectionNorm1, intersectionNorm2,
                           uvCoords[aloneIndex], intersectionUV1, intersectionUV2);
                
                // Other two vertices form quad on positive side
                AddTriangle(posVertices, posTriangles, posNormals, posUVs,
                           intersection1, verts[next1], verts[next2],
                           intersectionNorm1, norms[next1], norms[next2],
                           intersectionUV1, uvCoords[next1], uvCoords[next2]);
                
                AddTriangle(posVertices, posTriangles, posNormals, posUVs,
                           intersection1, verts[next2], intersection2,
                           intersectionNorm1, norms[next2], intersectionNorm2,
                           intersectionUV1, uvCoords[next2], intersectionUV2);
            }
        }
    }
    
    private static Mesh CreateMesh(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs)
    {
        if (vertices.Count == 0) return null;
        
        var mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        
        mesh.RecalculateBounds();
        return mesh;
    }
}