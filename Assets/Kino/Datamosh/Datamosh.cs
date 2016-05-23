﻿//
// Kino/Datamosh - Video compression artifact effect
//
// Copyright (C) 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using UnityEngine;

namespace Kino
{
    [RequireComponent(typeof(Camera))]
    public class Datamosh : MonoBehaviour
    {
        #region Public properties and methods

        /// Size of compression macroblock.
        public int blockSize {
            get { return Mathf.Max(4, _blockSize) ; }
            set { _blockSize = value; }
        }

        [SerializeField]
        [Tooltip("Size of compression macroblock.")]
        int _blockSize = 16;

        /// Scale factor for velocity vectors.
        public float velocityScale {
            get { return _velocityScale; }
            set { _velocityScale = value; }
        }

        [SerializeField]
        [Tooltip("Scale factor for velocity vectors.")]
        float _velocityScale = 0.8f;

        /// Start glitching.
        public void Glitch()
        {
            _sequence = 1;
        }

        /// Stop glitching.
        public void Reset()
        {
            _sequence = 0;
        }

        #endregion

        #region Private properties

        [SerializeField]
        Shader _shader;

        Material _material;

        RenderTexture _workBuffer; // working buffer
        RenderTexture _dispBuffer; // displacement buffer

        int _sequence;

        RenderTexture NewWorkBuffer(RenderTexture source)
        {
            var rt =  RenderTexture.GetTemporary(source.width, source.height);
            //rt.filterMode = FilterMode.Point;
            return rt;
        }

        RenderTexture NewDispBuffer(RenderTexture source)
        {
            var rt = RenderTexture.GetTemporary(
                source.width / _blockSize,
                source.height / _blockSize,
                0, RenderTextureFormat.ARGBHalf
            );
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        void ReleaseBuffer(RenderTexture buffer)
        {
            if (buffer != null) RenderTexture.ReleaseTemporary(buffer);
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            var shader = Shader.Find("Hidden/Kino/Datamosh");
            _material = new Material(shader);
            _material.hideFlags = HideFlags.DontSave;

            var camera = GetComponent<Camera>();
            camera.depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            _sequence = 0;
        }

        void OnDisable()
        {
            ReleaseBuffer(_workBuffer);
            _workBuffer = null;

            ReleaseBuffer(_dispBuffer);
            _dispBuffer = null;

            DestroyImmediate(_material);
            _material = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            _material.SetFloat("_BlockSize", _blockSize);
            _material.SetFloat("_VelocityScale", _velocityScale);

            if (_sequence == 0)
            {
                // Step 0: no effect, just keep the last frame.

                // Update the working buffer with the current frame.
                ReleaseBuffer(_workBuffer);
                _workBuffer = NewWorkBuffer(source);
                Graphics.Blit(source, _workBuffer);

                // Blit without effect.
                Graphics.Blit(source, destination);
            }
            else if (_sequence == 1)
            {
                // Step 1: start effect, no moshing.

                // Initialize the displacement buffer.
                ReleaseBuffer(_dispBuffer);
                _dispBuffer = NewDispBuffer(source);
                Graphics.Blit(null, _dispBuffer, _material, 0);

                // Simply blit the working buffer because motion vectors
                // might not be ready (because of camera switching).
                Graphics.Blit(_workBuffer, destination);

                _sequence++;
            }
            else
            {
                // Step 2: apply effect.

                // Update the displaceent buffer.
                var newDisp = NewDispBuffer(source);
                Graphics.Blit(_dispBuffer, newDisp, _material, 1);
                ReleaseBuffer(_dispBuffer);
                _dispBuffer = newDisp;

                // Moshing!
                var newWork = NewWorkBuffer(source);
                _material.SetTexture("_WorkTex", _workBuffer);
                _material.SetTexture("_DispTex", _dispBuffer);
                Graphics.Blit(source, newWork, _material, 2);
                ReleaseBuffer(_workBuffer);
                _workBuffer = newWork;

                // Blit the result.
                Graphics.Blit(_workBuffer, destination);
            }
        }

        #endregion
    }
}
