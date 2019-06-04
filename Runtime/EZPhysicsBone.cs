/* Author:          ezhex1991@outlook.com
 * CreateTime:      2018-12-18 19:33:50
 * Organization:    #ORGANIZATION#
 * Description:     
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EZUnity.PhysicsBone
{
    public class EZPhysicsBone : MonoBehaviour
    {
        public class TreeNode : IDisposable
        {
            public TreeNode parent;
            public TreeNode leftSibling;
            public TreeNode rightSibling;
            public List<TreeNode> children = new List<TreeNode>();

            public Transform transform;

            public int depth;
            public float nodeLength;
            public float boneLength;

            public float leftLength;
            public float rightLength;

            public float treeLength;
            public float normalizedLength;
            public float radius;

            public Vector3 position;
            public Vector3 speed;

            public Vector3 originalLocalPosition;
            public Quaternion originalLocalRotation = Quaternion.identity;

            public TreeNode() { }
            public TreeNode(Transform t, float endLength, int startDepth, int depth, float localLength, float parentLength)
            {
                transform = t;
                if (t != null)
                {
                    position = t.position;
                    originalLocalPosition = t.localPosition;
                    originalLocalRotation = t.localRotation;
                }
                this.depth = depth;
                nodeLength = localLength;
                if (depth > startDepth)
                {
                    boneLength = parentLength + nodeLength;
                }
                treeLength = boneLength;
                if (t.childCount > 0)
                {
                    for (int i = 0; i < t.childCount; i++)
                    {
                        Transform child = t.GetChild(i);
                        // local length should ignore parent scales
                        TreeNode node = new TreeNode(transform.GetChild(i), endLength, startDepth, depth + 1, (child.position - t.position).magnitude, boneLength);
                        node.parent = this;
                        children.Add(node);
                        treeLength = Mathf.Max(treeLength, node.treeLength);
                    }
                }
                else
                {
                    // end node
                    TreeNode node = new TreeNode();
                    if (t.parent != null)
                    {
                        // extend last bone to end node using local position
                        node.originalLocalPosition = t.InverseTransformVector(t.parent.TransformVector(t.localPosition)) * endLength;
                    }
                    node.depth = depth + 1;
                    node.position = t.TransformPoint(node.originalLocalPosition);
                    // nodeLength should be in world space
                    node.nodeLength = t.TransformVector(node.originalLocalPosition).magnitude;
                    node.boneLength = boneLength + node.nodeLength;
                    node.treeLength = node.boneLength;
                    node.parent = this;
                    children.Add(node);
                    treeLength = node.treeLength;
                }
            }

            public void SetLeftSibling(TreeNode node)
            {
                leftSibling = node;
                leftLength = (this.position - node.position).magnitude;
            }
            public void SetRightSibling(TreeNode node)
            {
                rightSibling = node;
                rightLength = (this.position - node.position).magnitude;
            }

            public void Inflate(float baseRadius, AnimationCurve radiusCurve)
            {
                if (treeLength <= 0) return;
                normalizedLength = boneLength / treeLength;
                radius = radiusCurve.Evaluate(normalizedLength) * baseRadius;
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Inflate(baseRadius, radiusCurve);
                }
            }

            public void RevertTransforms()
            {
                if (transform != null)
                {
                    transform.localPosition = originalLocalPosition;
                    transform.localRotation = originalLocalRotation;
                }
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].RevertTransforms();
                }
            }
            public void ApplyToTransform(bool recursive)
            {
                ApplyRotation();
                ApplyPosition();
                if (recursive)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].ApplyToTransform(recursive);
                    }
                }
            }
            public void ApplyRotation()
            {
                // rotate if has only one child, and this means transform could not be null too
                if (children.Count == 1)
                {
                    TreeNode child = children[0];
                    Quaternion rotation = Quaternion.FromToRotation(transform.TransformDirection(child.originalLocalPosition), child.position - position);
                    transform.rotation = rotation * transform.rotation;
                }
            }
            public void ApplyPosition()
            {
                if (transform != null)
                {
                    transform.position = position;
                }
            }
            public void SyncPosition(bool recursive)
            {
                if (transform != null)
                {
                    position = transform.position;
                }
                if (recursive)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        children[i].SyncPosition(recursive);
                    }
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Dispose();
                }
                parent = null;
                leftSibling = null;
                rightSibling = null;
                children.Clear();
                transform = null;
            }
        }

        [SerializeField]
        private List<Transform> m_RootBones;
        public List<Transform> rootBones { get { return m_RootBones; } }

        [SerializeField]
        private int m_StartDepth;
        public int startDepth { get { return m_StartDepth; } }

        [SerializeField]
        private bool m_UseSiblingConstraints;
        public bool useSiblingConstraints { get { return m_UseSiblingConstraints; } }

        [SerializeField]
        private float m_EndNodeLength;
        public float endNodeLength { get { return m_EndNodeLength; } }

        [SerializeField]
        private EZPhysicsBoneMaterial m_Material;
        public EZPhysicsBoneMaterial sharedMaterial
        {
            get
            {
                if (m_Material == null)
                    m_Material = EZPhysicsBoneMaterial.defaultMaterial;
                return m_Material;
            }
            set
            {
                m_Material = value;
            }
        }

        [SerializeField]
        private float m_SleepThreshold = 0.005f;
        public float sleepThreshold { get { return m_SleepThreshold; } set { m_SleepThreshold = Mathf.Max(0, value); } }

        [Header("Collision")]
        [SerializeField]
        private LayerMask m_CollisionLayers = 0;
        public LayerMask collisionLayers { get { return m_CollisionLayers; } }
        [SerializeField]
        private List<Collider> m_ExtraColliders = new List<Collider>();
        public List<Collider> extraColliders { get { return m_ExtraColliders; } }
        [SerializeField]
        private float m_Radius = 0;
        public float radius { get { return m_Radius; } }
        [SerializeField, EZCurveRange(0, 0, 1, 1)]
        private AnimationCurve m_RadiusCurve = AnimationCurve.Constant(0, 1, 1);
        public AnimationCurve radiusCurve { get { return m_RadiusCurve; } }

        [Header("Force")]
        [SerializeField]
        private Vector3 m_Gravity;
        public Vector3 gravity { get { return m_Gravity; } set { m_Gravity = value; } }
        [SerializeField]
        private EZPhysicsBoneForce m_ForceModule;
        public EZPhysicsBoneForce forceModule { get { return m_ForceModule; } set { m_ForceModule = value; } }

        public float globalRadius { get; private set; }
        private List<TreeNode> m_PhysicsTrees = new List<TreeNode>();

        private void Start()
        {
            InitPhysicsTrees();
        }
        private void OnEnable()
        {
            ResyncPhysicsTrees();
        }
        private void LateUpdate()
        {
            RevertTransforms();
            UpdatePhysicsTrees(Time.deltaTime);
            ApplyPhysicsTrees();
        }
        private void OnDisable()
        {
            RevertTransforms();
        }

        private void OnValidate()
        {
            m_StartDepth = Mathf.Max(0, m_StartDepth);
            m_EndNodeLength = Mathf.Max(0, m_EndNodeLength);
            m_SleepThreshold = Mathf.Max(0, m_SleepThreshold);
            m_Radius = Mathf.Max(0, m_Radius);
            if (Application.isEditor && Application.isPlaying)
            {
                RevertTransforms();
                InitPhysicsTrees();
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (!enabled) return;

            if (Application.isEditor && !Application.isPlaying && transform.hasChanged)
            {
                InitPhysicsTrees();
            }

            for (int i = 0; i < m_PhysicsTrees.Count; i++)
            {
                DrawNodeGizmos(m_PhysicsTrees[i]);
            }
        }

        private void InitPhysicsTrees()
        {
            m_PhysicsTrees.Clear();
            if (rootBones == null || rootBones.Count == 0) return;
            globalRadius = transform.lossyScale.Abs().Max() * radius;
            for (int i = 0; i < rootBones.Count; i++)
            {
                if (rootBones[i] == null) continue;
                TreeNode tree = new TreeNode(rootBones[i], endNodeLength, startDepth, 0, 0, 0);
                tree.Inflate(globalRadius, radiusCurve);
                m_PhysicsTrees.Add(tree);
            }
            if (useSiblingConstraints)
            {
                SetSiblings();
            }
        }
        private void SetSiblings()
        {
            TreeNode[] currentTier = new TreeNode[m_PhysicsTrees.Count];
            for (int i = 0; i < currentTier.Length; i++)
            {
                currentTier[i] = m_PhysicsTrees[i];
            }
            while (currentTier[0].children.Count > 0)
            {
                for (int i = 0; i < currentTier.Length; i++)
                {
                    int left = (i + currentTier.Length - 1) % currentTier.Length;
                    int right = (i + 1) % currentTier.Length;
                    currentTier[i].SetLeftSibling(currentTier[left]);
                    currentTier[i].SetRightSibling(currentTier[right]);
                }
                for (int i = 0; i < currentTier.Length; i++)
                {
                    currentTier[i] = currentTier[i].children[0];
                }
            }
        }

        private void RevertTransforms()
        {
            for (int i = 0; i < m_PhysicsTrees.Count; i++)
            {
                m_PhysicsTrees[i].RevertTransforms();
            }
        }
        private void ResyncPhysicsTrees()
        {
            for (int i = 0; i < m_PhysicsTrees.Count; i++)
            {
                m_PhysicsTrees[i].SyncPosition(true);
            }
        }

        private void UpdatePhysicsTrees(float deltaTime)
        {
            for (int i = 0; i < m_PhysicsTrees.Count; i++)
            {
                UpdateNode(m_PhysicsTrees[i], deltaTime);
            }
        }
        private void UpdateNode(TreeNode node, float deltaTime)
        {
            if (node.depth > startDepth)
            {
                Vector3 lastPosition = node.position;

                // Damping (inertia attenuation)
                if (node.speed.sqrMagnitude < sleepThreshold)
                {
                    node.speed = Vector3.zero;
                }
                else
                {
                    node.position += node.speed * deltaTime * (1 - sharedMaterial.GetDamping(node.normalizedLength));
                }

                // Resistance (outside force resistance)
                Vector3 force = gravity;
                if (forceModule != null)
                {
                    force += forceModule.GetForce(node.normalizedLength);
                }
                force.x *= transform.localScale.x;
                force.y *= transform.localScale.y;
                force.z *= transform.localScale.z;
                node.position += force * (1 - sharedMaterial.GetResistance(node.normalizedLength));

                // Stiffness (shape keeper)
                Vector3 parentOffset = node.parent.position - node.parent.transform.position;
                Vector3 expectedPos = node.parent.transform.TransformPoint(node.originalLocalPosition) + parentOffset;
                node.position = Vector3.Lerp(node.position, expectedPos, sharedMaterial.GetStiffness(node.normalizedLength));

                // Collision
                if (node.radius > 0)
                {
                    foreach (EZPhysicsBoneColliderBase collider in EZPhysicsBoneColliderBase.EnabledColliders)
                    {
                        if (node.transform != collider.transform && collisionLayers.Contains(collider.gameObject.layer))
                            collider.Collide(ref node.position, node.radius);
                    }
                    foreach (Collider collider in extraColliders)
                    {
                        if (node.transform != collider.transform && collider.enabled)
                            EZPhysicsBoneUtility.PointOutsideCollider(ref node.position, collider, node.radius);
                    }
                }

                // Slackness (length keeper)
                float slackness = sharedMaterial.GetSlackness(node.normalizedLength);
                Vector3 nodeDir = (node.position - node.parent.position).normalized;
                Vector3 lengthKeeper = node.parent.position + nodeDir * node.nodeLength;
                node.position = Vector3.Lerp(lengthKeeper, node.position, slackness);

                node.speed = (node.position - lastPosition) / deltaTime;
            }
            else
            {
                node.position = node.transform.position;
            }

            for (int i = 0; i < node.children.Count; i++)
            {
                UpdateNode(node.children[i], deltaTime);
            }
        }

        private void ApplyPhysicsTrees()
        {
            for (int i = 0; i < m_PhysicsTrees.Count; i++)
            {
                ApplyTransform(m_PhysicsTrees[i]);
            }
        }
        private void ApplyTransform(TreeNode node)
        {
            if (node.depth == startDepth)
            {
                node.ApplyRotation();
            }
            else if (node.depth > startDepth)
            {
                node.ApplyToTransform(false);
            }
            for (int i = 0; i < node.children.Count; i++)
            {
                ApplyTransform(node.children[i]);
            }
        }

        private void DrawNodeGizmos(TreeNode node)
        {
            for (int i = 0; i < node.children.Count; i++)
            {
                DrawNodeGizmos(node.children[i]);
            }
            Gizmos.color = Color.Lerp(Color.white, Color.red, node.normalizedLength);
            if (node.depth > startDepth)
                Gizmos.DrawWireSphere(node.position, node.radius);
            if (node.parent != null)
                Gizmos.DrawLine(node.parent.position, node.position);
            if (useSiblingConstraints)
            {
                if (node.leftSibling != null)
                    Gizmos.DrawLine(node.leftSibling.position, node.position);
                if (node.rightSibling != null)
                    Gizmos.DrawLine(node.rightSibling.position, node.position);
            }
        }
    }
}
