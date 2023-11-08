import openai 
import argparse

def main():
    # Usage: python3 sample.py <api_key>	
    parser = argparse.ArgumentParser(description="api key for gpt-3.5 turbo")
    parser.add_argument("api_key", type=str)
    args = parser.parse_args()

    openai.api_key = args.api_key
    messages = [ {"role": "system", "content": 
                "You are a intelligent assistant."} ] 
    while True: 
        message = input("User : ") 
        if message: 
            messages.append( 
                {"role": "user", "content": message}, 
            ) 
            chat = openai.ChatCompletion.create( 
                model="gpt-3.5-turbo", messages=messages 
            ) 
        reply = chat.choices[0].message.content 
        print(f"ChatGPT: {reply}") 
        messages.append({"role": "assistant", "content": reply}) 

if __name__ == "__main__":
    main()
