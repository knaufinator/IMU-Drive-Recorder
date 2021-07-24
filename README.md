# IMU Drive Recorder
## Record your Drive - Feel it again 

This is a WPF (MVVM) application that will record IMU acceleration and orientation data and allow you to sync this data with a video, while playing it back on your Motion Simulator. 
This app requires the use of OBS studio to record the video feeds. 

The app will record the data to a SQLite database at around 1000 HZ, from the IMU, the playback is done at 100 hz, but could be modified as needed. the data is queried in real time based on the interpolated position of the video. so no optimization of the data is needed.