from mpu6050 import mpu6050
import time
import math
import sys
import socket
import json

sensor = mpu6050(0x68)

IP = sys.argv[1]
PORT = sys.argv[2]

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

server_address = (IP, int(PORT))
print(sys.stderr, 'sensor connecting to %s port %s' % server_address)
sock.connect(server_address)

data = ''
    
try:    
    while data != 'CLOSE':
        # get raw accel data
        accel = sensor.get_accel_data()

        # adjust accelerometer values to (-1 - 1)
        magnitude = math.sqrt((accel['x'] * accel['x']) + (accel['y'] * accel['y']) + (accel['z'] * accel['z']))

        accel['x'] = accel['x'] / magnitude
        accel['y'] = accel['y'] / magnitude
        accel['z'] = accel['z'] / magnitude

        print(' ax = ', ( accel['x'] ))
        print(' ay = ', ( accel['y'] ))
        print(' az = ', ( accel['z'] ))

        # create json
        json = {
            'accel': accel
        }
        
        print(json)
        
        # send accelerometer data as json
        message = str(json).replace("'", "\"")
        print(sys.stderr, 'sending "%s"' % message)
        sock.send(message.encode())

        # wait for response back from server
        sock.recv(64).decode()

        time.sleep(0.05)
except e as Exception:
    print(sys.stderr, e)
finally:
    print(sys.stderr, 'sensor: closing socket')
    sock.close()

