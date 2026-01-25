using System.Text.Json;
using NotesAvalonia.Models;

public static class ExampleNote
{
  public static Note[]? Example => JsonSerializer.Deserialize<Note[]>(@"[
    {
      ""Done"": false,
      ""Text"": ""Active"",
      ""Expanded"": true,
      ""SubNotes"": [
        {
          ""Done"": false,
          ""Text"": ""Auto Versicherung?!"",
          ""Expanded"": true,
          ""SubNotes"": [
            {
              ""Done"": true,
              ""Text"": ""Papa ist weiter verantwortlich"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Warten auf neue eVB"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Einkaufen "",
          ""Expanded"": true,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Käse 2"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Fruchtsaft 1"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Milka"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Hörnchen?"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Toast? "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        }
      ],
      ""Prio"": 2
    },
    {
      ""Done"": false,
      ""Text"": ""Backlog"",
      ""Expanded"": true,
      ""SubNotes"": [
        {
          ""Done"": false,
          ""Text"": ""Versuchen unbezahlten Urlaub im März 2026 zu beantragen?"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Zum Fahrradfahren mitnehmen"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Decke zum hinlegen auf dem waldboden"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Wasser"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": true,
          ""Text"": ""Vivaldi make addessbar background dark gray again"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": true,
          ""Text"": ""Fix keyboard switches double trigger "",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Programming"",
          ""Expanded"": true,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Update notes to learn avalonia ui, websockets and keycloak "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Port musicplayer to bevy"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": true,
              ""Text"": ""Build Remote controller app in capacitor js to watch movies with"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Try out nixos deploy on AWS again"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Wled Orchestartor Server bauen"",
              ""Expanded"": true,
              ""SubNotes"": [
                {
                  ""Done"": false,
                  ""Text"": ""Try out nwag client"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": true,
                  ""Text"": ""Wochentagsbeschränkung in Modifier"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""SVG overlay with times"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": true,
                  ""Text"": ""Host on nixos"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                }
              ],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        }
      ],
      ""Prio"": 2
    },
    {
      ""Done"": false,
      ""Text"": ""Nice To Have"",
      ""Expanded"": false,
      ""SubNotes"": [
        {
          ""Done"": false,
          ""Text"": ""Mantra"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""FLS"",
              ""Expanded"": false,
              ""SubNotes"": [
                {
                  ""Done"": false,
                  ""Text"": ""Never forget what Marc Peer and Jasha did to you. They abused your will to fit in to form you into another person. Never forget how awful the resulting depression was after my mother started being an asshole too. I literally wanted to die for months! These people owe you nothing. They are disgusting hollow bastards."",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Never forget Peers "",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                }
              ],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Stay away from most people, do your own thing"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Ich habe Wert "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""I am allowed to take up space "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Ich bin genug"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""While I grew up with a mother that does not respect me I need to respect myself instead and remove myself from unnecessary situations "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Tipps für mehr Energie"",
              ""Expanded"": false,
              ""SubNotes"": [
                {
                  ""Done"": false,
                  ""Text"": ""Vergiss den context von deinem Leben so gut es geht, put pressure on it so none may enter, nur an das zu denken was man Grade tut ist besser und macht glücklicher "",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Denke nicht zu detailliert an die Zukunft, es gibt immer neue unbekannte, spare lieber jetzt die energie und denke an die Zukunft nur vague und optimistisch wie ein Kind "",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                }
              ],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Meine Gefühle sind valide"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Mein Bauch weiß was gut für mich ist "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""I can show myself and my needs"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Meine Perspektive ist wichtig"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Ich kann mehr Kontrolle haben als ich denke (just leave)"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""I and anything I do doesnt have to be perfect or even close to it, I just need to keep moving forward without worries if I want to live right"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Your mental health and physical well-being are more valuable than any job."",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Don't let yourself be controlled by your fears, handle them, alleviate them, get a healing mindset "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Fears aren't always obvious, take your time to examine yourself"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Don't forget chakra based meditation"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Never do anything unnecessary that gets you below low energy"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Be VERY careful with your energy levels"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Jetzigen Gedanken-Status ausquetschen und optimistisch in die Zukunft gucken "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Andere haben nicht das Recht mir schmerzen zuzuführen, ich existiere nicht um wiederholt verletzt zu werden sondern darf auch Mal was von anderen verlangen, ich darf meinen Platz in sinnvollen Maßen einnehmen "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Game mechanics based on the human experience"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""A heart burning with passion, less sleep/regen but more dmg"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""A shield that blocks most incoming damage (~85%) but also blocks the view of your hp bar, like blocking out unwelcome emotions "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""3"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Ich Ehre meine Identität"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Ich bin bereit loszulassen"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Meine Erfahrungen sind wahr, meine Gefühle haben einen guten Grund "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Hobby Programming"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Notes"",
              ""Expanded"": false,
              ""SubNotes"": [
                {
                  ""Done"": false,
                  ""Text"": ""When dragging a note on mobile scroll on screen edges"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Fix desktop not having crossed out notes on startup"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Add single note change endpoint"",
                  ""Expanded"": true,
                  ""SubNotes"": [
                    {
                      ""Done"": false,
                      ""Text"": ""Add note Ids"",
                      ""Expanded"": false,
                      ""SubNotes"": [],
                      ""Prio"": 2
                    }
                  ],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Save notes as urlencode"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Show number of subnotes on expand button"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Change newline note creation rules such that note is split on curser and the other way around for deletion "",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""spoilered notes"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Add automated note trashcan"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Add note links"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": true,
                  ""Text"": ""Notes Desktop => retry connection on fail / show connection state"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Hash the password in the server env vars"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Notes just save edited date for each note to implement eventual consistency"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Notes changed webhook?"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                }
              ],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""MusicPlayer"",
              ""Expanded"": false,
              ""SubNotes"": [
                {
                  ""Done"": false,
                  ""Text"": ""music player add mp3 thumbnail to chapter downloads (taglib?)"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""music player bulk song ranaming / deleting"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""music player fix song renaming"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Music Player check if downloader update worked"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                },
                {
                  ""Done"": false,
                  ""Text"": ""Music Player fix init fake focus"",
                  ""Expanded"": false,
                  ""SubNotes"": [],
                  ""Prio"": 2
                }
              ],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Add Licht Wecker functionality to wled controller and put on pine64 "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Neonshooter check linux install "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Rust Raylib Particle System"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Port Platformer to Monogame"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""SC Replay Vis"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Nach Masterarbeit: Zimmer, überschüssiges zuegs wegschmeißen"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""drawabox ausprobieren"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Buy"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""dimmable bicycle light?"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Fahrrad neuer bowdenzug, Gangschaltungsdisplay, Korb, Mantel vorne "",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Camera? 📷"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Xbox Controller?"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""NAS in mein Zimmer"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Gamecube in die schachtel tun"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Nach zimmerschlüssel suchen "",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""Watch/Read/Play"",
          ""Expanded"": false,
          ""SubNotes"": [
            {
              ""Done"": false,
              ""Text"": ""Alita Manga"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Vampire Hunter D manga"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Angels Egg"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Chainsawmn manga"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Jujutsu Kaisen weiter"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""The Lighthouse"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""Tokyo Ghoul manga"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            },
            {
              ""Done"": false,
              ""Text"": ""https://en.m.wikipedia.org/wiki/I've_Been_Killing_Slimes_for_300_Years_and_Maxed_Out_My_Level"",
              ""Expanded"": false,
              ""SubNotes"": [],
              ""Prio"": 2
            }
          ],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""MA Purge am: 19.12. 14:25"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""W?"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        },
        {
          ""Done"": false,
          ""Text"": ""F?"",
          ""Expanded"": false,
          ""SubNotes"": [],
          ""Prio"": 2
        }
      ],
      ""Prio"": 2
    }
  ]");
}