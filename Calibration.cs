using Emgu.CV;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using PointF = System.Drawing.PointF;

namespace Microsoft.Samples.Kinect.ControlsBasics
{
    /// <summary>
    /// Maps a 3D skeleton position to a 2D position on the stage (bird's-eye view of the floor).
    /// Collect <see cref="RequiredPoints"/> pairs with <see cref="AddPoint"/>, call <see cref="Calibrate"/>,
    /// then use <see cref="ToStage"/>.
    /// </summary>
    public class Calibration
    {
        public const int RequiredPoints = 4;

        private readonly CoordinateMapper mapper;
        private readonly List<Point> screenPoints = new List<Point>();
        private readonly List<SkeletonPoint> skeletonPoints = new List<SkeletonPoint>();

        private Matrix3D flattenPlane;       // step 2: floor plane -> XY plane
        private Matrix<double> homography;   // step 3: XY plane -> stage

        public bool IsCalibrated { get; private set; }

        public Calibration(CoordinateMapper mapper)
        {
            this.mapper = mapper;
        }

        public void Reset()
        {
            screenPoints.Clear();
            skeletonPoints.Clear();
            IsCalibrated = false;
        }

        /// <summary>Stores one (stage point, skeleton position) pair. Returns false if full or the depth is invalid.</summary>
        public bool AddPoint(Point screenPoint, SkeletonPoint skeletonPoint)
        {
            if (screenPoints.Count >= RequiredPoints || ToDepthSpace(skeletonPoint).Z <= 0) return false;
            screenPoints.Add(screenPoint);
            skeletonPoints.Add(skeletonPoint);
            return true;
        }

        /// <summary>Runs the calibration. Returns the max reprojection error in stage units.</summary>
        public double Calibrate()
        {
            if (screenPoints.Count != RequiredPoints)
                throw new InvalidOperationException("Need exactly " + RequiredPoints + " calibration points");

            // Step 1: skeleton positions -> positions in the depth camera
            Point3D[] depth = skeletonPoints.Select(ToDepthSpace).ToArray();

            // Step 2: rotate the floor plane (spanned by the first three points) onto the XY plane
            Vector3D edgeX = depth[1] - depth[0];
            Vector3D edgeY = depth[2] - depth[0];
            edgeX.Normalize();
            edgeY.Normalize();
            Vector3D normal = Vector3D.CrossProduct(edgeX, edgeY);
            if (normal.Length < 1e-6)
                throw new InvalidOperationException("Calibration points are in a line");
            flattenPlane = RotateOntoZAxis(normal);

            // Step 3: perspective transform from the flattened 2D points to the stage rectangle
            PointF[] source = depth.Select(Flatten).ToArray();
            PointF[] target = screenPoints.Select(p => new PointF((float)p.X, (float)p.Y)).ToArray();
            using (Mat transform = CvInvoke.GetPerspectiveTransform(source, target))
            {
                homography = new Matrix<double>(transform.Rows, transform.Cols, transform.NumberOfChannels);
                transform.CopyTo(homography);
            }
            IsCalibrated = true;

            // The four calibration points should map back onto their targets.
            return Enumerable.Range(0, RequiredPoints)
                             .Max(i => (ToStage(skeletonPoints[i]) - screenPoints[i]).Length);
        }

        /// <summary>Any 3D skeleton position -> 2D position on the stage.</summary>
        public Point ToStage(SkeletonPoint point)
        {
            PointF[] result = CvInvoke.PerspectiveTransform(new[] { Flatten(ToDepthSpace(point)) }, homography);
            return new Point(result[0].X, result[0].Y);
        }

        private Point3D ToDepthSpace(SkeletonPoint point)
        {
            DepthImagePoint p = mapper.MapSkeletonPointToDepthPoint(point, KinectService.DepthFormat);
            return new Point3D(p.X, p.Y, p.Depth);
        }

        private PointF Flatten(Point3D point)
        {
            Point3D p = flattenPlane.Transform(point);
            return new PointF((float)p.X, (float)p.Y);
        }

        /// <summary>Rotation that turns <paramref name="direction"/> into the +Z axis.</summary>
        private static Matrix3D RotateOntoZAxis(Vector3D direction)
        {
            var z = new Vector3D(0, 0, 1);
            Vector3D axis = Vector3D.CrossProduct(direction, z);
            double angle = Vector3D.AngleBetween(direction, z); // degrees

            if (axis.LengthSquared < 1e-12)                      // already parallel to Z
            {
                if (angle < 90) return Matrix3D.Identity;
                axis = new Vector3D(1, 0, 0);                    // pointing -Z: flip around any perpendicular axis
            }
            axis.Normalize();

            Matrix3D rotation = Matrix3D.Identity;
            rotation.Rotate(new Quaternion(axis, angle));
            return rotation;
        }
    }
}
