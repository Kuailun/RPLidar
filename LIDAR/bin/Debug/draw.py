import json
import cv2
import numpy as np
import os

#Read the config
with open("./config.json",'r') as config:
    mConfig=json.load(config)
    print(mConfig["COM"])
    config.close()

def drawRect(img,lines):
    retImg=img
    for i in range(len(lines)):
        limit=mConfig["Segmentation"]
        m=i//limit
        n=i%limit
        size=1024/mConfig["Segmentation"]
        ns=int(n*size)
        ms=int(m*size)
        #contour=np.array([[[0,0],[0,10],[10,10],[10,0]]],dtype=np.int32)
        contour=np.array([[[ns,ms],[ns,ms+size],[ns+size,ms+size],[ns+size,ms]]],dtype=np.int32)

        if(lines[i]>=8):
            retImg = cv2.fillPoly(retImg, contour, (0, 0, 255))
            #retImg=cv2.rectangle(retImg,(int(n*size),int(m*size)),(int(n*size+size),int(m*size+size)),(0,0,255),-2,2)
        elif(lines[i]>=5):
            retImg = cv2.fillPoly(retImg, contour, (0, 255, 255))
            #retImg = cv2.rectangle(retImg, (int(n * size), int(m * size)), (int(n * size + size), int(m * size + size)),(0, 255, 255), 2, 2)
        elif (lines[i] >= 1):
            retImg = cv2.fillPoly(retImg, contour, (0, 255, 0))
            #retImg = cv2.rectangle(retImg, (int(n * size), int(m * size)), (int(n * size + size), int(m * size + size)),(0, 255, 0), 2, 2)
    return retImg
#Get the fileList
fileList=os.listdir("./Database")

for i in range(len(fileList)):
    str2="./Database/"+fileList[i]
    with open(str2) as data:
        lines = data.readline()
        while lines:
            strData = lines.split(" ")
            strData.remove("\n")
            intData=strData[1:len(strData)-1]
            intData = list(map(int, intData))
            img = np.zeros((1024, 1024, 3), dtype=np.uint8)
            img2display = drawRect(img, intData)
            cv2.imshow("Data", img2display)
            cv2.waitKey(0)

            lines = data.readline()