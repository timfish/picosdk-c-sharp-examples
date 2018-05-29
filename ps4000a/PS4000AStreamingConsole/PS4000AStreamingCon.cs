/******************************************************************************
 *
 *  Filename: PS4000AStreamingCon.cs
 *  
 *  Description:
 *    This is a console-mode program that demonstrates how to use the
 *    ps4000a driver API functions using .NET
 *    
 *  Supported PicoScope models:
 *
 *		PicoScope 4444
 *		PicoScope 4824
 *		
 *  Examples:
 *     Collect a stream of data immediately
 *     Collect a stream of data when a trigger event occurs
 *    
 *  Copyright © 2015-2018 Pico Technology Ltd. See LICENSE file for terms.
 *
 ******************************************************************************/

using System;
using System.IO;
using System.Text;
using System.Threading;

using PS4000AImports;
using PicoStatus;
using PicoPinnedArray;

namespace PS4000AStreamingConsole
{
    struct ChannelSettings
    {
        public Imports.Range range;
        public bool enabled;
    }

    class StreamingConSole
    {
        private readonly short _handle;
        int _channelCount;
        private ChannelSettings[] _channelSettings;


        short[][] appBuffers;
        short[][] buffers;

        uint[] inputRanges = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000 };
        bool _ready = false;
        short _trig = 0;
        uint _trigAt = 0;
        int _sampleCount;
        uint _startIndex;
        bool _autoStop;
        short _maxValue;

        public Imports.Range _firstRange;
        public Imports.Range _lastRange;

        /****************************************************************************
        * Callback
        * Used by PS4000a data streaming collection calls, on receipt of data.
        * Used to set global flags etc checked by user routines
        ****************************************************************************/
        void StreamingCallback(short handle,
                                int noOfSamples,
                                uint startIndex,
                                short overflow,
                                uint triggerAt,
                                short triggered,
                                short autoStop,
                                IntPtr pVoid)
        {
            // used for streaming
            _sampleCount = noOfSamples;
            _startIndex = startIndex;
            _autoStop = autoStop != 0;

            _ready = true;

            // flags to show if & where a trigger has occurred
            _trig = triggered;
            _trigAt = triggerAt;

            if (_sampleCount != 0)
            {
               Array.Copy(buffers[0], _startIndex, appBuffers[0], _startIndex, _sampleCount); //max
            }
        }


        /****************************************************************************
       * Stream Data Handler
       * - Used by the two stream data examples - untriggered and triggered
       * Inputs:
       * - unit - the unit to sample on
       * - preTrigger - the number of samples in the pre-trigger phase 
       *					(0 if no trigger has been set)
       ***************************************************************************/
        void StreamDataHandler(uint preTrigger)
        {
            int tempBufferSize = 100 * 100;
            _channelCount = 1;

            appBuffers = new short[_channelCount][];
            buffers = new short[_channelCount][];

            short setAutoStop = 0;
            
            uint totalSamples = 0;
            uint triggeredAt = 0;
            uint sampleInterval = 1;
            uint downSampleRatio = 1;
            uint status;
            uint maxPostTriggerSamples =  1000 - preTrigger;

            // Use Pinned Arrays for the application buffers
            PinnedArray<short>[] appBuffersPinned = new PinnedArray<short>[_channelCount];

            buffers[0] = new short[tempBufferSize];
            appBuffers[0] = new short[tempBufferSize];
            appBuffersPinned[0] = new PinnedArray<short>(appBuffers[0]);

            status = Imports.SetDataBuffer(_handle, (Imports.Channel)(0), buffers[0], tempBufferSize, 0, Imports.DownSamplingMode.None);

            status = Imports.RunStreaming(_handle, ref sampleInterval, Imports.ReportedTimeUnits.MilliSeconds, preTrigger, maxPostTriggerSamples, setAutoStop, downSampleRatio, 
                                                Imports.DownSamplingMode.None, (uint)tempBufferSize);
            
            while (!Console.KeyAvailable)
            {
                /* Poll until data is received. Until then, GetStreamingLatestValues wont call the callback */
                Thread.Sleep(0);
                _ready = false;
                status = Imports.GetStreamingLatestValues(_handle, StreamingCallback, IntPtr.Zero);

                if (_ready && _sampleCount > 0) /* can be ready and have no data, if autoStop has fired */
                {
                    totalSamples += (uint) _sampleCount;
                    Console.Write("\nCollected {0,4} samples, index = {1,5}, Total = {2,5}", _sampleCount, _startIndex, totalSamples);
                }
            }

            if (Console.KeyAvailable)
            {
                Console.ReadKey(true); // clear the key
            }

            Imports.Stop(_handle);
        }


        private StreamingConSole(short handle)
        {
            _handle = handle;
        }

        static void Main()
        {
            uint status = 0;


            // Open unit and show splash screen
            Console.WriteLine("\n\nOpening a device...");
            short handle;
            status = Imports.OpenUnit(out handle, null);
            Console.WriteLine("Handle: {0}", handle);

            if (status != StatusCodes.PICO_OK && handle != 0)
            {
                status = Imports.ps4000aChangePowerSource(handle, status);
            }

            if (status != StatusCodes.PICO_OK)
            {
                Console.WriteLine("Unable to open device");
                Console.WriteLine("Error code : {0}", status);
                return;
            }
 
            Console.WriteLine("Device opened successfully\n");

            Imports.SetSimpleTrigger(handle, 0, Imports.Channel.CHANNEL_A, 0, Imports.ThresholdDirection.None, 0, 0);

            StreamingConSole consoleExample = new StreamingConSole(handle);
            consoleExample.StreamDataHandler(0);

            Imports.CloseUnit(handle);

        }
    }
}
