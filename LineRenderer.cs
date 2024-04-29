using Godot;
using System;

public partial class LineRenderer : MeshInstance3D
{
    [Export]
    public Vector3[] points = { new Vector3(0, 0, 0), new Vector3(0, 5, 0) };
    [Export]
    public float startThickness = 0.1f;
    [Export]
    public float endThickness = 0.1f;
    [Export]
    public int cornerSmooth = 5;
    [Export]
    public int capSmooth = 5;
    [Export]
    public bool drawCaps = true;
    [Export]
    public bool drawCorners = true;
    [Export]
    public bool globalCoords = true;
    [Export]
    public bool scaleTexture = true;

	[Export]
	public Material material;

    private Camera3D camera;
    private Vector3 cameraOrigin;

	ImmediateMesh mesh;

    public override void _Ready()
    {
		if(Mesh == null)
		{
			Mesh = new ImmediateMesh();
		}

		mesh = (ImmediateMesh)Mesh;
    }

    public override void _Process(double delta)
    {
        if (points.Length < 2)
        {
            return;
        }

        camera = GetViewport().GetCamera3D();
        if (camera == null)
        {
            return;
        }

        cameraOrigin = ToLocal(camera.GlobalTransform.Origin);

        float progressStep = 1.0f / points.Length;
        float progress = 0;
        float thickness = Mathf.Lerp(startThickness, endThickness, progress);
        float nextThickness = Mathf.Lerp(startThickness, endThickness, progress + progressStep);

        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);


        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 A = points[i];
            Vector3 B = points[i + 1];

            if (globalCoords)
            {
                A = ToLocal(A);
                B = ToLocal(B);
            }

            Vector3 AB = B - A;
            Vector3 orthogonalABStart = (cameraOrigin - ((A + B) / 2)).Cross(AB).Normalized() * thickness;
            Vector3 orthogonalABEnd = (cameraOrigin - ((A + B) / 2)).Cross(AB).Normalized() * nextThickness;

            Vector3 AtoABStart = A + orthogonalABStart;
            Vector3 AfromABStart = A - orthogonalABStart;
            Vector3 BtoABEnd = B + orthogonalABEnd;
            Vector3 BfromABEnd = B - orthogonalABEnd;

            if (i == 0 && drawCaps)
            {
                Cap(A, B, thickness, capSmooth);
            }

            if (scaleTexture)
            {
                float ABLen = AB.Length();
                float ABFloor = Mathf.Floor(ABLen);
                float ABFrac = ABLen - ABFloor;

                mesh.SurfaceSetUV(new Vector2(ABFloor, 0));
                mesh.SurfaceAddVertex(AtoABStart);
                mesh.SurfaceSetUV(new Vector2(-ABFrac, 0));
                mesh.SurfaceAddVertex(BtoABEnd);
                mesh.SurfaceSetUV(new Vector2(ABFloor, 1));
                mesh.SurfaceAddVertex(AfromABStart);
                mesh.SurfaceSetUV(new Vector2(-ABFrac, 0));
                mesh.SurfaceAddVertex(BtoABEnd);
                mesh.SurfaceSetUV(new Vector2(-ABFrac, 1));
                mesh.SurfaceAddVertex(BfromABEnd);
                mesh.SurfaceSetUV(new Vector2(ABFloor, 1));
                mesh.SurfaceAddVertex(AfromABStart);
            }
            else
            {
                mesh.SurfaceSetUV(new Vector2(1, 0));
                mesh.SurfaceAddVertex(AtoABStart);
                mesh.SurfaceSetUV(new Vector2(0, 0));
                mesh.SurfaceAddVertex(BtoABEnd);
                mesh.SurfaceSetUV(new Vector2(1, 1));
                mesh.SurfaceAddVertex(AfromABStart);
                mesh.SurfaceSetUV(new Vector2(0, 0));
                mesh.SurfaceAddVertex(BtoABEnd);
                mesh.SurfaceSetUV(new Vector2(0, 1));
                mesh.SurfaceAddVertex(BfromABEnd);
                mesh.SurfaceSetUV(new Vector2(1, 1));
                mesh.SurfaceAddVertex(AfromABStart);
            }

            if (i == points.Length - 2)
            {
                if (drawCaps)
                {
                    Cap(B, A, nextThickness, capSmooth);
                }
            }
            else
            {
                if (drawCorners)
                {
                    Vector3 C = points[i + 2];
                    if (globalCoords)
                    {
                        C = ToLocal(C);
                    }

                    Vector3 BC = C - B;
                    Vector3 orthogonalBCStart = (cameraOrigin - ((B + C) / 2)).Cross(BC).Normalized() * nextThickness;

                    float angleDot = AB.Dot(orthogonalBCStart);

                    if (angleDot > 0)
                    {
                        Corner(B, BtoABEnd, B + orthogonalBCStart, cornerSmooth);
                    }
                    else
                    {
                        Corner(B, B - orthogonalBCStart, BfromABEnd, cornerSmooth);
                    }
                }
            }

            progress += progressStep;
            thickness = Mathf.Lerp(startThickness, endThickness, progress);
            nextThickness = Mathf.Lerp(startThickness, endThickness, progress + progressStep);
        }

        mesh.SurfaceEnd();
    }

    private void Cap(Vector3 center, Vector3 pivot, float thickness, int smoothing)
    {
        Vector3 orthogonal = (cameraOrigin - center).Cross(center - pivot).Normalized() * thickness;
        Vector3 axis = (center - cameraOrigin).Normalized();

        Vector3[] array = new Vector3[smoothing + 1];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = Vector3.Zero;
        }
        array[0] = center + orthogonal;
        array[smoothing] = center - orthogonal;

        for (int i = 1; i < smoothing; i++)
        {
            array[i] = center + (orthogonal.Rotated(axis, Mathf.Lerp(0.0f, Mathf.Pi, (float)i / smoothing)));
        }

        for (int i = 1; i <= smoothing; i++)
        {
            mesh.SurfaceSetUV(new Vector2(0, (i - 1) / (float)smoothing));
            mesh.SurfaceAddVertex(array[i - 1]);
            mesh.SurfaceSetUV(new Vector2(0, (i - 1) / (float)smoothing));
            mesh.SurfaceAddVertex(array[i]);
            mesh.SurfaceSetUV(new Vector2(0.5f, 0.5f));
            mesh.SurfaceAddVertex(center);
        }
    }

	
    private void Corner(Vector3 center, Vector3 start, Vector3 end, int smoothing)
    {
        Vector3[] array = new Vector3[smoothing + 1];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = Vector3.Zero;
        }
        array[0] = start;
        array[smoothing] = end;
		Vector3 axis = start.Cross(end).Normalized();
        Vector3 offset = start - center;
        float angle = offset.AngleTo(end - center);

        for (int i = 1; i < smoothing; i++)
        {
            array[i] = center + offset.Rotated(axis, Mathf.Lerp(0.0f, angle, (float)i / smoothing));
        }

        for (int i = 1; i <= smoothing; i++)
        {
            mesh.SurfaceSetUV(new Vector2(0, (i - 1) / (float)smoothing));
            mesh.SurfaceAddVertex(array[i - 1]);
            mesh.SurfaceSetUV(new Vector2(0, (i - 1) / (float)smoothing));
            mesh.SurfaceAddVertex(array[i]);
            mesh.SurfaceSetUV(new Vector2(0.5f, 0.5f));
            mesh.SurfaceAddVertex(center);
        }
    }
}