# IMU Drive Recorder
## Record your Drive - Feel it again 

This is a WPF (MVVM) application that will record IMU acceleration and orientation data from https://github.com/knaufinator/ESP32-IMU-Orientation and allow you to sync this data with a video, while playing it back on your Motion Simulator. This also allows the real time control of a motion sim with an IMU in Real time but this was not its intended use. ... just a neat side effect...
This app requires the use of OBS studio to record the video feeds. 

The app will record the data to a SQLite database at around 1000 HZ, from the IMU, the playback is done at 100 hz, but could be modified as needed. the data is queried in real time based on the interpolated position of the video. so no optimization of the data is needed.

The SimTools Game plugin is also included, this will need to be compiled and installed into simtools using the plugin updater. This will allow the Drive recorder app to comunicate to simtools and allow motion.

OBD2 access is also enabled, I use a OBDLink mx bluetooth adapter to get access to the car ECU. I have found the common cheap chinease ones did not work as nice,.. but YRMV.