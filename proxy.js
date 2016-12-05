var Buffer = require('buffer').Buffer;
var dgram = require('dgram');
var WebSocketServer = require('ws').Server;
 
var wss = new WebSocketServer({port: 8081});
 
//The ip &amp; port of the udp server
var SERVER_IP = '127.0.0.1'
var SERVER_PORT = 4559
 
wss.on('connection', function(ws) {
    console.log("connected");
    //Create a udp socket for this websocket connection
    var udpClient = dgram.createSocket('udp4');
 
    //When a message is received from udp server send it to the ws client
    udpClient.on('message', function(msg, rinfo) {
        console.log(msg.toString());
        console.log(rinfo);
        ws.send(msg.toString());
    });

    udpClient.on('error', function(error) {
        console.log(error);
    });  

    udpClient.on('ready', function(error) {
        console.log("ready");
    });  

    udpClient.on('close', function(error) {
        console.log("close");
    });            
    //When a message is received from ws client send it to udp server.
    ws.on('message', function(message) {
        console.log("sending message to udp");
        var msgBuff = new Buffer(message);
        udpClient.send(msgBuff, 0, msgBuff.length, SERVER_PORT, SERVER_IP, (err) => {
        console.log(err);
        //udpClient.close();
      }); 
    });
});