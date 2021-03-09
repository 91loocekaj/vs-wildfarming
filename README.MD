-----Plant System-----
To create a new plant to grow a wildseeds and wildplant of the block must be put as a part of the code for both. For example say I wanted to grow a bowl. I would create a wildseeds variant with the code "wildseeds-bowl-burned" and a wildplant variant with the code "wildplant-bowl-burned".

-----Attributes of Wild Plants, Seaweed, and Vines-----
minTemp = A float which is the minimum temperature this plant can tolerate in Celsius
maxTemp = A float which is the maximum temperature this plant can tolerate in Celsius
hours = A float which is the average amount of hours it takes to grow a plant (Note: Every plant growth time is randomized around this value by +-25%)

-----Pine Log Scoring Behavior Attributes-----
scoreTime = The (float)amount of time it takes to cut the Log
scoringSound = A string representing the path to the sound file that should play when scoring
