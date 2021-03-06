﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AudioLib;
using Tobi.Common.MVVM;
using urakawa.core;
using urakawa.data;
using urakawa.media.data.audio;

namespace Tobi.Plugin.AudioPane
{
    public partial class AudioPaneViewModel
    {
        [NotifyDependsOn("IsRecording")]
        [NotifyDependsOn("IsMonitoringAlways")]
        [NotifyDependsOn("IsMonitoring")]
        [NotifyDependsOn("IsWaveFormLoading")]
        public bool CanManipulateWaveForm
        {
            get
            {
                return (!IsMonitoring || IsMonitoringAlways)
                    && !IsRecording
                    && !IsWaveFormLoading
                    && State.Audio.HasContent;
            }
        }

        public class StreamStateData
        {
            private AudioPaneViewModel m_viewModel;
            private PropertyChangedNotifyBase m_notifier;
            public StreamStateData(PropertyChangedNotifyBase notifier, AudioPaneViewModel vm)
            {
                m_viewModel = vm;
                m_notifier = notifier;
            }

            public bool HasContent
            {
                get { return PlayStream != null; }
            }


            private Stream SetPlayStream_FromTreeNode_OPEN(Stream stream)
            {
                if (stream != null)
                {
                    stream.Position = 0;
                    stream.Seek(0, SeekOrigin.Begin);

                    Tuple<TreeNode, TreeNode> treeNodeSelection = m_viewModel.m_UrakawaSession.GetTreeNodeSelection();

                    if (treeNodeSelection.Item1 != null)
                    {
                        DebugFix.Assert(treeNodeSelection.Item1.Presentation.MediaDataManager.EnforceSinglePCMFormat);
                        PcmFormat = treeNodeSelection.Item1.Presentation.MediaDataManager.DefaultPCMFormat;
                    }
                    else if (m_viewModel.m_UrakawaSession != null && m_viewModel.m_UrakawaSession.DocumentProject != null)
                    {
                        DebugFix.Assert(m_viewModel.m_UrakawaSession.DocumentProject.Presentations.Get(0).MediaDataManager.EnforceSinglePCMFormat);
                        PcmFormat = m_viewModel.m_UrakawaSession.DocumentProject.Presentations.Get(0).MediaDataManager.DefaultPCMFormat;
                    }
                    else
                    {
                        PcmFormat = null;
                        Debug.Fail("This should never happen !!");
                    }

                    DataLength = stream.Length;
                    EndOffsetOfPlayStream = DataLength;
                }

                return stream;
            }

            public void SetPlayStream_FromTreeNode(
                Stream stream,
                Stream secondaryStream
                )
            {
                PlayStream = SetPlayStream_FromTreeNode_OPEN(stream);
                m_SecondaryAudioStream = SetPlayStream_FromTreeNode_OPEN(secondaryStream);
            }

            private Stream SetPlayStream_FromFile_OPEN(
                FileStream fileStream,
                string filePathOptionalInfo)
            {
                Stream stream = fileStream;

                if (stream != null)
                {
                    stream.Position = 0;
                    stream.Seek(0, SeekOrigin.Begin);

                    uint dataLength;
                    AudioLibPCMFormat format = AudioLibPCMFormat.RiffHeaderParse(stream, out dataLength);

                    PcmFormat = new PCMFormatInfo(format);

                    dataLength = (uint)(stream.Length - stream.Position);

                    stream = new SubStream(stream, stream.Position, dataLength, filePathOptionalInfo);

                    DebugFix.Assert(dataLength == stream.Length);

                    DataLength = stream.Length;
                    EndOffsetOfPlayStream = DataLength;
                }

                return stream;
            }

            public void SetPlayStream_FromFile(
                FileStream fileStream,
                FileStream secondaryFileStream,
                string filePathOptionalInfo)
            {
                PlayStream = SetPlayStream_FromFile_OPEN(fileStream, filePathOptionalInfo);
                m_SecondaryAudioStream = SetPlayStream_FromFile_OPEN(secondaryFileStream, filePathOptionalInfo);
            }

            // The single stream of contiguous PCM data,
            // regardless of the sub chunks / tree nodes
            private Stream m_PlayStream;
            public Stream PlayStream
            {
                get
                {
                    return m_PlayStream;
                }
                set
                {
                    if (m_PlayStream == value) return;
                    m_PlayStream = value;
                    m_notifier.RaisePropertyChanged(() => PlayStream);
                }
            }

            private Stream m_SecondaryAudioStream;
            public Stream SecondaryAudioStream
            {
                get { return m_SecondaryAudioStream; }
            }

            // The total byte length of the stream of audio PCM data.
            private long m_DataLength;
            public long DataLength
            {
                get
                {
                    return m_DataLength;
                }
                set
                {
                    if (m_DataLength == value) return;
                    m_DataLength = value;
                    m_notifier.RaisePropertyChanged(() => DataLength);
                }
            }

            // The PCM format of the stream of audio data.
            // Can have a valid value even when the stream is null (e.g. when recording or inserting an external audio file)
            private PCMFormatInfo m_PcmFormat;
            public PCMFormatInfo PcmFormat
            {
                get
                {
                    return m_PcmFormat;
                }
                set
                {
                    if (m_PcmFormat == value) return;

                    if (value == null
                        && (m_viewModel.IsPlaying || m_viewModel.m_UrakawaSession.DocumentProject != null || m_viewModel.State.FilePath != null))
                    {
                        //Debugger.Break();
                        bool debug = true;
                    }

                    m_PcmFormat = value;
                    m_notifier.RaisePropertyChanged(() => PcmFormat);
                }
            }

            // Used when recording or monitoring (no loaded stream data yet, just the PCM information)
            private PCMFormatInfo m_PcmFormatRecordingMonitoring;
            public PCMFormatInfo PcmFormatRecordingMonitoring
            {
                get
                {
                    return m_PcmFormatRecordingMonitoring;
                }
                set
                {
                    if (m_PcmFormatRecordingMonitoring == value) return;

                    if (value == null
                        && (m_viewModel.IsRecording || m_viewModel.IsMonitoring))
                    {
                        //Debugger.Break();
                        bool debug = true;
                    }

                    m_PcmFormatRecordingMonitoring = value;
                    m_notifier.RaisePropertyChanged(() => PcmFormatRecordingMonitoring);
                }
            }

            public PCMFormatInfo GetCurrentPcmFormat()
            {
                if (m_viewModel.IsRecording || m_viewModel.IsMonitoring)
                {
                    if (PcmFormatRecordingMonitoring == null)
                    {
                        PcmFormatRecordingMonitoring = new PCMFormatInfo(m_viewModel.m_Recorder.RecordingPCMFormat);
                    }
                    return PcmFormatRecordingMonitoring;
                }
                if (m_viewModel.IsPlaying)
                {
                    if (PcmFormat == null)
                    {
                        PcmFormat = new PCMFormatInfo(m_viewModel.m_Player.CurrentAudioPCMFormat);
                    }
                }
                if (PcmFormat == null && m_viewModel.m_UrakawaSession.DocumentProject != null)
                {
                    PcmFormat = m_viewModel.m_UrakawaSession.DocumentProject
                        .Presentations.Get(0).MediaDataManager.DefaultPCMFormat.Copy();
                }
                return PcmFormat;
            }


            // The stream offset in bytes where the audio playback should stop.
            // By default: it is the DataLength, but it can be changed when dealing with selections and preview-playback modes.
            private long m_EndOffsetOfPlayStream;
            public long EndOffsetOfPlayStream
            {
                get
                {
                    return m_EndOffsetOfPlayStream;
                }
                set
                {
                    if (m_EndOffsetOfPlayStream == value) return;
                    m_EndOffsetOfPlayStream = value;
                    m_notifier.RaisePropertyChanged(() => EndOffsetOfPlayStream);
                }
            }

            // The list that defines the sub treenodes with associated chunks of audio data
            // This is never null: the count is 1 when the current main tree node has direct audio (no sub tree nodes)

            private
#if USE_NORMAL_LIST
            List
#else
 LightLinkedList
#endif //USE_NORMAL_LIST
<TreeNodeAndStreamDataLength> m_PlayStreamMarkers;
            public
#if USE_NORMAL_LIST
            List
#else
 LightLinkedList
#endif //USE_NORMAL_LIST
<TreeNodeAndStreamDataLength> PlayStreamMarkers
            {
                get
                {
                    return m_PlayStreamMarkers;
                }
                set
                {
                    if (m_PlayStreamMarkers == value) return;
                    if (value == null)
                    {
                        m_PlayStreamMarkers.Clear();
                    }
                    m_PlayStreamMarkers = value;
                    m_notifier.RaisePropertyChanged(() => PlayStreamMarkers);
                }
            }

            public void ResetAll()
            {
                m_SecondaryAudioStream = null;
                PlayStream = null; // must be first because NotifyPropertyChange chain-reacts for DataLength (TimeString data binding) 

                EndOffsetOfPlayStream = -1;
                PcmFormat = null;

                if (!Settings.Default.Audio_DisableSingleWavFileRecord)
                {
                    //DebugFix.Assert(m_UrakawaSession.DocumentProject.Presentations.Get(0).MediaDataManager.EnforceSinglePCMFormat);
                    if (m_viewModel != null
                        && m_viewModel.m_RecordAndContinue
                        && m_viewModel.m_UrakawaSession != null
                        && m_viewModel.m_UrakawaSession.DocumentProject != null)
                    {
                        PcmFormatRecordingMonitoring =
                            m_viewModel.m_UrakawaSession.DocumentProject.Presentations.Get(0).MediaDataManager.DefaultPCMFormat;
                    }
                    else
                    {
                        PcmFormatRecordingMonitoring = null;
                    }
                }
                else
                {
                    PcmFormatRecordingMonitoring = null;
                }



                PlayStreamMarkers = null;
                DataLength = -1;

                m_notifier.RaisePropertyChanged(() => m_viewModel.CanManipulateWaveForm);
            }

            public bool FindInPlayStreamMarkers(TreeNode treeNode, out int index, out long bytesLeft, out long bytesRight)
            {
                bytesRight = 0;
                bytesLeft = 0;
                index = -1;
                if (PlayStreamMarkers == null) return false;

#if USE_NORMAL_LIST
                foreach (TreeNodeAndStreamDataLength marker in PlayStreamMarkers)
                {
#else
                LightLinkedList<TreeNodeAndStreamDataLength>.Item current = PlayStreamMarkers.m_First;
                while (current != null)
                {
                    TreeNodeAndStreamDataLength marker = current.m_data;
#endif //USE_NORMAL_LIST

                    index++;
                    bytesRight += marker.m_LocalStreamDataLength;
                    if (treeNode == marker.m_TreeNode || treeNode.IsDescendantOf(marker.m_TreeNode))
                    {
                        return true;
                    }
                    bytesLeft = bytesRight;

#if USE_NORMAL_LIST
                }
#else
                    current = current.m_nextItem;
                }
#endif //USE_NORMAL_LIST


                return false;
            }

            public bool FindInPlayStreamMarkers(long byteOffset, out TreeNode treeNode, out int index, out long bytesLeft, out long bytesRight)
            {
                treeNode = null;
                bytesRight = 0;
                bytesLeft = 0;
                index = -1;
                if (PlayStreamMarkers == null) return false;

#if USE_NORMAL_LIST
                foreach (TreeNodeAndStreamDataLength marker in PlayStreamMarkers)
                {
#else
                LightLinkedList<TreeNodeAndStreamDataLength>.Item current = PlayStreamMarkers.m_First;
                while (current != null)
                {
                    TreeNodeAndStreamDataLength marker = current.m_data;
#endif //USE_NORMAL_LIST

                    index++;
                    bytesRight += marker.m_LocalStreamDataLength;
                    if (byteOffset < bytesRight
                    || index == (PlayStreamMarkers.Count - 1) && byteOffset >= bytesRight)
                    {
                        treeNode = marker.m_TreeNode;

                        return true;
                    }
                    bytesLeft = bytesRight;

#if USE_NORMAL_LIST
                }
#else
                    current = current.m_nextItem;
                }
#endif //USE_NORMAL_LIST

                return false;
            }

            public void FindInPlayStreamMarkersAndDo(long byteOffset,
                Func<long, long, TreeNode, int, long> matchFunc,
                Func<long, long, long, TreeNode, long> nonMatchFunc)
            {
                if (PlayStreamMarkers == null) return;

                TreeNode treeNode = null;
                long bytesRight = 0;
                long bytesLeft = 0;
                int index = -1;

#if USE_NORMAL_LIST
                foreach (TreeNodeAndStreamDataLength marker in PlayStreamMarkers)
                {
#else
                LightLinkedList<TreeNodeAndStreamDataLength>.Item current = PlayStreamMarkers.m_First;
                while (current != null)
                {
                    TreeNodeAndStreamDataLength marker = current.m_data;
#endif //USE_NORMAL_LIST

                    treeNode = marker.m_TreeNode;

                    index++;
                    bytesRight += marker.m_LocalStreamDataLength;
                    if (byteOffset < bytesRight
                    || index == (PlayStreamMarkers.Count - 1) && byteOffset >= bytesRight)
                    {
                        long newMatch = matchFunc(bytesLeft, bytesRight, treeNode, index);
                        if (newMatch == -1)
                        {
                            break;
                        }
                        byteOffset = newMatch;
                    }
                    else
                    {
                        long newMatch = nonMatchFunc(byteOffset, bytesLeft, bytesRight, treeNode);
                        if (newMatch == -1)
                        {
                            break;
                        }
                        byteOffset = newMatch;
                    }
                    bytesLeft = bytesRight;

#if USE_NORMAL_LIST
                }
#else
                    current = current.m_nextItem;
                }
#endif //USE_NORMAL_LIST
            }

            //public bool IsTreeNodeShownInAudioWaveForm(TreeNode treeNode)
            //{
            //    if (treeNode == null)
            //    {
            //        return false;
            //    }

            //    Tuple<TreeNode, TreeNode> treeNodeSelection = m_viewModel.m_UrakawaSession.GetTreeNodeSelection();
            //    if (treeNodeSelection.Item1 == treeNode
            //        || treeNodeSelection.Item2 == treeNode)
            //    {
            //        return true;
            //    }

            //    if (PlayStreamMarkers == null)
            //    {
            //        return false;
            //    }

            //    long bytesRight;
            //    long bytesLeft;
            //    int index;
            //    return FindInPlayStreamMarkers(treeNode, out index, out bytesLeft, out bytesRight);
            //}
        }

        public class SelectionStateData
        {
            private AudioPaneViewModel m_viewModel;
            private PropertyChangedNotifyBase m_notifier;
            public SelectionStateData(PropertyChangedNotifyBase notifier, AudioPaneViewModel vm)
            {
                m_notifier = notifier;
                m_viewModel = vm;
            }

            public void SetSelectionBytes(long begin, long end)
            {
                m_viewModel.SetRecordAfterPlayOverwriteSelection(-1);

                if (m_viewModel.View != null && m_viewModel.State.Audio.HasContent)
                {
                    SelectionBeginBytePosition = m_viewModel.State.Audio.GetCurrentPcmFormat().Data.AdjustByteToBlockAlignFrameSize(begin);
                    SelectionEndBytePosition = m_viewModel.State.Audio.GetCurrentPcmFormat().Data.AdjustByteToBlockAlignFrameSize(end);

                    m_viewModel.View.SetSelectionBytes(SelectionBeginBytePosition, SelectionEndBytePosition);
                }
                else
                {
                    SelectionBeginBytePosition = begin;
                    SelectionEndBytePosition = end;
                }

                if (true || m_viewModel.IsAutoPlay)
                {
                    m_viewModel.PlayBytePosition = SelectionBeginBytePosition;


                    //m_viewModel.m_LastSetPlayHeadTime = SelectionBegin;
                    //m_viewModel.CommandPlay.Execute();



                    //long bytesFrom = Convert.ToInt64(m_TimeSelectionLeftX * BytesPerPixel);

                    //m_ViewModel.IsAutoPlay = false;
                    //m_ViewModel.LastPlayHeadTime = m_ViewModel.State.Audio.ConvertBytesToMilliseconds(bytesFrom);
                    //m_ViewModel.IsAutoPlay = true;

                    //long bytesTo = Convert.ToInt64(right * BytesPerPixel);

                    //m_ViewModel.AudioPlayer_PlayFromTo(bytesFrom, bytesTo);
                }
            }

            public void ClearSelection()
            {
                m_viewModel.SetRecordAfterPlayOverwriteSelection(-1);

                SelectionBeginBytePosition = -1;
                SelectionEndBytePosition = -1;
                if (m_viewModel.View != null)
                {
                    m_viewModel.View.ClearSelection();
                }
            }

            private long m_SelectionBeginBytePosition;
            public long SelectionBeginBytePosition
            {
                get
                {
                    return m_SelectionBeginBytePosition;
                }
                private set
                {
                    if (m_SelectionBeginBytePosition == value) return;
                    m_SelectionBeginBytePosition = value;
                    m_notifier.RaisePropertyChanged(() => SelectionBeginBytePosition);
                }
            }

            private long m_SelectionEndVytePosition;
            public long SelectionEndBytePosition
            {
                get
                {
                    return m_SelectionEndVytePosition;
                }
                private set
                {
                    if (m_SelectionEndVytePosition == value) return;
                    m_SelectionEndVytePosition = value;
                    m_notifier.RaisePropertyChanged(() => SelectionEndBytePosition);
                }
            }

            public void ResetAll()
            {
                SelectionBeginBytePosition = -1;
                SelectionEndBytePosition = -1;
            }
        }

        public StateData State { get; private set; }
        public class StateData
        {
            public SelectionStateData Selection { get; private set; }
            public StreamStateData Audio { get; private set; }

            private AudioPaneViewModel m_viewModel;
            private PropertyChangedNotifyBase m_notifier;
            public StateData(PropertyChangedNotifyBase notifier, AudioPaneViewModel vm)
            {
                m_notifier = notifier;
                m_viewModel = vm;
                Audio = new StreamStateData(m_notifier, vm);
                Selection = new SelectionStateData(m_notifier, vm);
            }


            //// Main selected node. There are sub tree nodes when no audio is directly
            //// attached to this tree node.
            //// Automatically implies that FilePath is null
            //// (they are mutually-exclusive state values).
            //private TreeNode m_CurrentTreeNode;
            //public TreeNode CurrentTreeNode
            //{
            //    get
            //    {
            //        return m_CurrentTreeNode;
            //    }
            //    set
            //    {
            //        if (m_CurrentTreeNode == value) return;
            //        m_CurrentTreeNode = value;
            //        m_notifier.RaisePropertyChanged(() => CurrentTreeNode);

            //        CurrentSubTreeNode = null;

            //        FilePath = null;
            //    }
            //}

            //// Secondary selected node. By default is the first one in the series.
            //// It is equal to the main selected tree node when the audio data is attached directly to it.
            //private TreeNode m_CurrentSubTreeNode;
            //public TreeNode CurrentSubTreeNode
            //{
            //    get
            //    {
            //        return m_CurrentSubTreeNode;
            //    }
            //    set
            //    {
            //        if (m_CurrentSubTreeNode == value) return;
            //        m_CurrentSubTreeNode = value;
            //        m_notifier.RaisePropertyChanged(() => CurrentSubTreeNode);
            //    }
            //}

            // Path to a WAV file,
            // only used when the user opens such file for playback / preview.
            // Automatically implies that CurrentTreeNode and CurrentSubTreeNode are null
            // (they are mutually-exclusive state values).
            private string m_WavFilePath;
            public string FilePath
            {
                get
                {
                    return m_WavFilePath;
                }
                set
                {
                    if (m_WavFilePath == value) return;
                    m_WavFilePath = value;
                    m_notifier.RaisePropertyChanged(() => FilePath);

                    //CurrentTreeNode = null;
                }
            }

            public void ResetAll()
            {
                //m_viewModel.Logger.Log("Audio StateData reset.", Category.Debug, Priority.Medium);

                FilePath = null;
                //CurrentTreeNode = null;
                //CurrentSubTreeNode = null;

                Selection.ResetAll();
                Audio.ResetAll();

                if (m_viewModel.View != null)
                {
                    m_viewModel.View.ResetAll();
                }
            }
        }
    }
}
