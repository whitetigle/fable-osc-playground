# fable-osc-playground
Fable project for testing the sending of osc messages to sonic-pi 

- `npm install` to install dependencies.
- `npm run build` of `fable` to compile the F# code to JS. + watch
- `npm start` to fire up a local server and open the sample web page.
- `npm run sync` to launch browser sync.
- `npm start proxy` to start our websocket/udp proxy

## Try it!
1. Open Sonic-Pi Gui
2. Open music.rb in Sonic-Pi
3. Start playing the score
4. Start ws/udp proxy with `npm start proxy`
5. build the project using `npm run build`
6. serve index.html with `npm start`
7. browse to http://localhost:4000
7. Et voilà! You should hear random notes popping every 1/2 sec

## Notes
- This project a simple proof of concept to interact with Sonic-Pi from a web page.
- The proxy file (Js at the moment, F# later :smile:) allows the web page to send osc commands via a web socket. The proxy then forwards the osc message to Sonic PI via udp (localhost port 4559)


## Credits
- loading bar from http://loading.io/
- osc Javascript lib from https://github.com/colinbdclark/osc.js/
- sonic pi score from https://gist.github.com/rbnpi/ca5e80258c0c1296b1c6c3974060b3ce
- websocket/udp proxy code updated from https://dimitrisfousteris.wordpress.com/2014/12/07/websocket-udp-proxy-with-node-js/

Made with :heart: Code by @whitetigle 

Powered by @fable-compiler :rocket: 

and sonic-pi from @samaaron :sparkles: