import os
import numpy as np
import json
import xlsxwriter

#Read the config
with open("./config.json",'r') as config:
    mConfig=json.load(config)
    seg=mConfig["Segmentation"]
    config.close()

with open("./GridData.geojson","w") as gridData:
    gD={}
    gD["type"]="FeatureCollection"
    features=[]
    for i in range(seg*seg):
        featuresData={}

        ID=i
        unitX=mConfig["Width"]/seg/1000
        unitY=mConfig["Length"]/seg/1000
        X1=(i%seg)*unitX
        Y1=(i//seg)*unitY
        X2=X1+unitX
        Y2=Y1+unitY

        featuresData["type"] = "Feature"
        featuresData["properties"]={"ID":ID}

        geometry={}
        geometry["type"]="MultiPolygon"

        coordinates=[]
        coordinates.append([X1,Y1])
        coordinates.append([X1, Y2])
        coordinates.append([X2, Y2])
        coordinates.append([X2, Y1])
        coordinates.append([X1, Y1])
        geometry["coordinates"]=[coordinates]
        featuresData["geometry"]=geometry

        features.append(featuresData)
    gD["features"]=features
    gridData.write(json.dumps(gD))
#Get the fileList
fileList=os.listdir("./Database")

for i in range(len(fileList)):
    name=fileList[i]
    name=name.replace(".txt","")
    str1="./Database/"+fileList[i]
    str2="./ConvertedDatabase/"+name+".xlsx"

    test_book = xlsxwriter.Workbook(str2)
    worksheet=test_book.add_worksheet("sheet1")
    worksheet.write(0,0,"ID")
    for i in range(seg*seg):
        worksheet.write(i+1,0,i)
    with open(str1) as rawdata:
        lines = rawdata.readline()
        cnt=0
        while lines:
            cnt=cnt+1
            strData = lines.split(" ")
            strData.remove("\n")
            worksheet.write(0, cnt,strData[0] )
            workData=strData[1:len(strData)]
            for j in range(len(workData)):
                worksheet.write(j+1,cnt,workData[j])

            lines = rawdata.readline()
    test_book.close()