﻿using System;
using System.Audio;
using BrawlLib.SSBBTypes;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BrawlLib.Wii.Audio
{
    public static class RSTMConverter
    {
        public static unsafe byte[] Encode(IAudioStream stream, IProgressTracker progress)
        {
            int tmp;
            bool looped = stream.IsLooping;
            int channels = stream.Channels;
            int samples;
            int blocks;
            int sampleRate = stream.Frequency;
            int lbSamples, lbSize, lbTotal;
            int loopPadding, loopStart, totalSamples;
            short* tPtr;

            int samplesPerBlock = 0x3800;

            if (looped)
            {
                loopStart = stream.LoopStartSample;
                samples = stream.LoopEndSample; //Set sample size to end sample. That way the audio gets cut off when encoding.

                //If loop point doesn't land on a block, pad the stream so that it does.
                if ((tmp = loopStart % samplesPerBlock) != 0)
                {
                    loopPadding = samplesPerBlock - tmp;
                    loopStart += loopPadding;
                }
                else
                    loopPadding = 0;
                
                totalSamples = loopPadding + samples;
            }
            else
            {
                loopPadding = loopStart = 0;
                totalSamples = samples = stream.Samples;
            }

            if (progress != null)
                progress.Begin(0, totalSamples * channels * 3, 0);

            blocks = (totalSamples + samplesPerBlock - 1) / samplesPerBlock;

            //Initialize stream info
            if ((tmp = totalSamples % samplesPerBlock) != 0)
            {
                lbSamples = tmp;
                lbSize = (lbSamples + 13) / 14 * 8;
                lbTotal = lbSize.Align(0x20);
            }
            else
            {
                lbSamples = samplesPerBlock;
                lbTotal = lbSize = 0x2000;
            }

            //Get section sizes
            int rstmSize = 0x40;
            int headSize = (0x68 + (channels * 0x40)).Align(0x20);
            int adpcSize = ((blocks - 1) * 4 * channels + 0x10).Align(0x20);
            int dataSize = ((blocks - 1) * 0x2000 + lbTotal) * channels + 0x20;
            
            //Create file map
            byte[] map = new byte[rstmSize + headSize + adpcSize + dataSize];
            fixed (byte* address = map)
            {
                //Get section pointers
                RSTMHeader* rstm = (RSTMHeader*)address;
                HEADHeader* head = (HEADHeader*)((byte*)rstm + rstmSize);
                ADPCHeader* adpc = (ADPCHeader*)((byte*)head + headSize);
                RSTMDATAHeader* data = (RSTMDATAHeader*)((byte*)adpc + adpcSize);

                //Initialize sections
                rstm->Set(headSize, adpcSize, dataSize);
                head->Set(headSize, channels);
                if (adpcSize > 0)
                    adpc->Set(adpcSize);
                data->Set(dataSize);

                //Set HEAD data
                StrmDataInfo* part1 = head->Part1;
                part1->_format = new AudioFormatInfo(2, (byte)(looped ? 1 : 0), (byte)channels, 0);
                part1->_sampleRate = (ushort)sampleRate;
                part1->_blockHeaderOffset = 0;
                part1->_loopStartSample = loopStart;
                part1->_numSamples = totalSamples;
                part1->_dataOffset = rstmSize + headSize + adpcSize + 0x20;
                part1->_numBlocks = blocks;
                part1->_blockSize = 0x2000;
                part1->_samplesPerBlock = samplesPerBlock;
                part1->_lastBlockSize = lbSize;
                part1->_lastBlockSamples = lbSamples;
                part1->_lastBlockTotal = lbTotal;
                part1->_dataInterval = samplesPerBlock;
                part1->_bitsPerSample = 4;

                //Create one ADPCMInfo for each channel
                int* adpcData = stackalloc int[channels];
                ADPCMInfo** pAdpcm = (ADPCMInfo**)adpcData;
                for (int i = 0; i < channels; i++)
                    *(pAdpcm[i] = head->GetChannelInfo(i)) = new ADPCMInfo() { _pad = 0 };

                //Create buffer for each channel
                int* bufferData = stackalloc int[channels];
                short** channelBuffers = (short**)bufferData;
                int bufferSamples = totalSamples + 2; //Add two samples for initial yn values
                for (int i = 0; i < channels; i++)
                {
                    channelBuffers[i] = tPtr = (short*)Marshal.AllocHGlobal(bufferSamples * 2); //Two bytes per sample

                    //Zero padding samples and initial yn values
                    for (int x = 0; x < (loopPadding + 2); x++)
                        *tPtr++ = 0;
                }

                //Fill buffers
                stream.SamplePosition = 0;
                short* sampleBuffer = stackalloc short[channels];

                for (int i = 2; i < bufferSamples; i++)
                {
                    if (stream.SamplePosition == stream.LoopEndSample && looped)
                        stream.SamplePosition = stream.LoopStartSample;

                    stream.ReadSamples(sampleBuffer, 1);
                    for (int x = 0; x < channels; x++)
                        channelBuffers[x][i] = sampleBuffer[x];
                }

                //Calculate coefs
                for (int i = 0; i < channels; i++)
                    AudioConverter.CalcCoefs(channelBuffers[i] + 2, totalSamples, (short*)pAdpcm[i], progress);

                //Encode blocks
                byte* dPtr = (byte*)data->Data;
                bshort* pyn = (bshort*)adpc->Data;
                for (int x = 0; x < channels; x++)
                {
                    *pyn++ = 0;
                    *pyn++ = 0;
                }

                for (int sIndex = 0, bIndex = 1; sIndex < totalSamples; sIndex += samplesPerBlock, bIndex++)
                {
                    int blockSamples = Math.Min(totalSamples - sIndex, samplesPerBlock);
                    for (int x = 0; x < channels; x++)
                    {
                        short* sPtr = channelBuffers[x] + sIndex;

                        //Set block yn values
                        if (bIndex != blocks)
                        {
                            *pyn++ = sPtr[samplesPerBlock + 1];
                            *pyn++ = sPtr[samplesPerBlock];
                        }

                        //Encode block (include yn in sPtr)
                        AudioConverter.EncodeBlock(sPtr, blockSamples, dPtr, (short*)pAdpcm[x]);

                        //Set initial ps
                        if (bIndex == 1)
                            pAdpcm[x]->_ps = *dPtr;

                        //Advance output pointer
                        if (bIndex == blocks)
                        {
                            //Fill remaining
                            dPtr += lbSize;
                            for (int i = lbSize; i < lbTotal; i++)
                                *dPtr++ = 0;
                        }
                        else
                            dPtr += 0x2000;
                    }

                    if (progress != null)
                    {
                        if ((sIndex % samplesPerBlock) == 0)
                            progress.Update(progress.CurrentValue + (0x7000 * channels));
                    }
                }

                //Reverse coefs
                for (int i = 0; i < channels; i++)
                {
                    short* p = pAdpcm[i]->_coefs;
                    for (int x = 0; x < 16; x++, p++)
                        *p = p->Reverse();
                }

                //Write loop states
                if (looped)
                {
                    //Can't we just use block states?
                    int loopBlock = loopStart / samplesPerBlock;
                    int loopChunk = (loopStart - (loopBlock * samplesPerBlock)) / 14;
                    dPtr = (byte*)data->Data + (loopBlock * 0x2000 * channels) + (loopChunk * 8);
                    tmp = (loopBlock == blocks - 1) ? lbTotal : 0x2000;

                    for (int i = 0; i < channels; i++, dPtr += tmp)
                    {
                        //Use adjusted samples for yn values
                        tPtr = channelBuffers[i] + loopStart;
                        pAdpcm[i]->_lps = *dPtr;
                        pAdpcm[i]->_lyn2 = *tPtr++;
                        pAdpcm[i]->_lyn1 = *tPtr;
                    }
                }

                //Free memory
                for (int i = 0; i < channels; i++)
                    Marshal.FreeHGlobal((IntPtr)channelBuffers[i]);

                if (progress != null)
                    progress.Finish();
            }

            return map;
        }
    }
}
