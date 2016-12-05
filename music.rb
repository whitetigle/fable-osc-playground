set_sched_ahead_time! 0
live_loop :kyb do
  as=sync "/kyb"  #incoming oosc messgage on /kbd sends note midi value as a parameter from kyeborad program
  n=as[:args][0]
  with_fx :reverb, room: 1 do
    synth :square  ,note: n,release: 0.1 #play the note with lots of reverb on square synth
  end
end
live_loop :r do #accopanying drone notes
  synth :tri,note: :c4,release: 0.4
  sleep 1
  synth :tri,note: :c3,release: 0.4
  sleep 1
end
live_loop :dr do #accompanying rhythm
  sample :bd_haus
  sleep 1
  sample :bd_haus
  sleep 0.75
  sample :bd_haus
  sleep 0.25
end