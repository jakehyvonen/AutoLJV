# AutoLJV
AutoLJV is a C#/.NET solution that controls instruments for voltage sweeps of LEDs. A WPF GUI allows users to interact with the instruments as well as "EFDeviceBatchCodeFirst" and "DeviceBatchGenerics" libraries to automatically generate reports of test data.

This project is designed to interact with:
<ul>
<li>a custom-built CNC system for loading and unloading test samples</li>
<li>a Raspberry Pi for taking pictures of the test pixels and handling electromechanical processes in concert with the CNC system</li>
<li>PhotoResearch cameras (PR650 and PR670) for luminance measurement</li>
<li>Keithley 2400 sourcemeters to supply voltage and measure LED current</li>
<li>Keithley 6485 picoammeters to measure photodiode current (and thereby, luminance)</li>
<li>a KUSB-488b adapter to interact with Keithley instruements via GPIB</li>
</ul>

The user first selects the DeviceBatch that should be tested by querying the MSSQL server via Entity Framework (using the EFDeviceBatchCodeFirst library). Then, they specify voltage sweep parameters and load the LED devices into the system. Finally, they tell the system to start the test and then it's simply a matter of waiting for the reports to be generated.

Output data includes:
<ul>
<li>Voltage-Luminance-Current raw data</li>
<li>Current Efficiency (CE), Power Efficiency (PE), External Quantum Efficiency (EQE)</li>
<li>Electroluminescent spectrum for every pixel measured</li>
<li>A picture of the pixel to evaluate uniformity</li>
<li>Percentage change between photodiode measurements at each voltage step to evaluate device stability on the fly</li>
</ul>

There are two measurements of LED light output: a fast measurement using a photodiode and the KE6485, then a slower (but precise) measurement with the PhotoResearch camera. To account for device instability, a photodiode reading is taken before and after the LED current measurement, before and after the camera measurement, and then interpolated. The camera luminance readings are calibrated against the photodiode readings

Please note that this project is not intended for general usage, but for reference only. Here are links to the other necessary libraries:
<ul>
<li><a href="https://github.com/jakehyvonen/EFDeviceBatchCodeFirst">EFDeviceBatchCodeFirst</a></li>
<li><a href="https://github.com/jakehyvonen/DeviceBatchGenerics">DeviceBatchGenerics</a></li>
<li><a href="https://github.com/jakehyvonen/ExtendedTreeView">ExtendedTreeView</a></li>
</ul>
The script running on the Raspberry Pi can be found here: <a href="https://github.com/jakehyvonen/BTSPython">BTSPython</a>
