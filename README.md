# bridged-token-ring
Written in college for a networking course. Simulator for bridged IBM token ring networks.

# 1 Compilation
One can either build in Visual Studio or use the provided MSBuild files with the Visual Studio developer command prompt. Ring, RingVariant, and Bridge are all compiled individually. To use the developer command prompt:

    MSBuild /Ring/Ring.csproj
    MSBuild /RingVariant/RingVariant.csproj
    MSBuild /Bridge/Bridge.csproj
    
# 2 Execution
There are sample input files in the io folder. Running a Ring/RingVariant executable takes in a ring config and a number of nodes. Running the Bridge just requires a log file. So if I were running a Ring and RingVariant with 250 total nodes, execution might look like this:

    /Ring/bin/Debug/Ring.exe /io/ring1.conf 250
    /RingVariant/bin/Debug/RingVariant.exe /io/ring2.conf 250
    /Bridge/bin/Debug/Bridge.exe /io/log.txt
    
This is really the shoddy bit. The big TODO is to make make a generator for the config files and for input to the socket. This may never be finished.
    
# 3 Subnets Don't Exit Automatically
When Bridge.exe stops pushing output to the console/log file, the simulation is complete. Use Ctrl-C.

# 4 Design Notes

## 4.1 Multiple Executables
Ensures discrete separation of subnets. Sockets are the only means of communication between executables.

## 4.2 Bridge Buffer Synchronization
The bridge utilizes two buffers.  Each corresponds to a specific ring.  As the bridge receives frames meant for a foreign ring, the bridge places the frames at the end of the targeted ring's buffer.

When a ring token arrives, the bridge will begin sending all the frames from that ring's buffer into the ring. This continues until the THT (token holding time) is exhausted or the buffer is empty, at which point the bridge enters its listen state again.

Frames that are sent to the same ring from which they came will be passed along with no buffer involvement after the bridge knows the network topology.

## 4.3 Node Threading
All nodes within each individual ring run as autonomous threads.

## 4.4. Protocol Mismatching
Ring and RingVariant operate on different protocols - thus the redundant code. The bridge converts protocol between them.

## 4.5 Dynamic Network Topology
The bridge starts with no knowledge of network topology, but keeps a routing table that updates as new nodes are discovered. The bridge floods the network when the destination is not known.
