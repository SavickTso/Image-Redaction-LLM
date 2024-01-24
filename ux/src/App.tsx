import {
  Button,
  Card,
  CardBody,
  CircularProgress,
  Container,
  Flex,
  Heading,
  Image,
  Input,
  InputGroup,
  InputRightElement,
  Text,
} from "@chakra-ui/react";
import ky from "ky";
import { useEffect, useState } from "react";

const base64ArrayBuffer = (arrayBuffer: ArrayBuffer) => {
  let base64 = "";
  const encodings =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

  const bytes = new Uint8Array(arrayBuffer);
  const byteLength = bytes.byteLength;
  const byteRemainder = byteLength % 3;
  const mainLength = byteLength - byteRemainder;

  let a, b, c, d;
  let chunk;

  // Main loop deals with bytes in chunks of 3
  for (let i = 0; i < mainLength; i = i + 3) {
    // Combine the three bytes into a single integer
    chunk = (bytes[i] << 16) | (bytes[i + 1] << 8) | bytes[i + 2];

    // Use bitmasks to extract 6-bit segments from the triplet
    a = (chunk & 16515072) >> 18; // 16515072 = (2^6 - 1) << 18
    b = (chunk & 258048) >> 12; // 258048   = (2^6 - 1) << 12
    c = (chunk & 4032) >> 6; // 4032     = (2^6 - 1) << 6
    d = chunk & 63; // 63       = 2^6 - 1

    // Convert the raw binary segments to the appropriate ASCII encoding
    base64 += encodings[a] + encodings[b] + encodings[c] + encodings[d];
  }

  // Deal with the remaining bytes and padding
  if (byteRemainder == 1) {
    chunk = bytes[mainLength];

    a = (chunk & 252) >> 2; // 252 = (2^6 - 1) << 2

    // Set the 4 least significant bits to zero
    b = (chunk & 3) << 4; // 3   = 2^2 - 1

    base64 += encodings[a] + encodings[b] + "==";
  } else if (byteRemainder == 2) {
    chunk = (bytes[mainLength] << 8) | bytes[mainLength + 1];

    a = (chunk & 64512) >> 10; // 64512 = (2^6 - 1) << 10
    b = (chunk & 1008) >> 4; // 1008  = (2^6 - 1) << 4

    // Set the 2 least significant bits to zero
    c = (chunk & 15) << 2; // 15    = 2^4 - 1

    base64 += encodings[a] + encodings[b] + encodings[c] + "=";
  }

  return base64;
};

function App() {
  const [original, setOriginal] = useState("");
  const [redacted, setRedacted] = useState("");
  const [taskId, setTaskId] = useState("");
  const [refine, setRefine] = useState("");

  useEffect(() => {
    if (taskId && !redacted) {
      const interval = setInterval(async () => {
        const res = await ky
          .post("/api/process_async_get?id=" + taskId)
          .json<{ image: string }>();
        setRedacted(res.image);
      }, 1000);
      return () => clearInterval(interval);
    }
  }, [taskId, redacted]);

  return (
    <Container background="gray.200" rounded="xl" height="100vh" padding="4">
      <Flex
        direction="column"
        justifyContent="flex-start"
        gap="4"
        height="100%"
        overflowY="scroll"
      >
        <Heading>IR-LLM</Heading>
        {!original && (
          <Card width="100%">
            <CardBody>
              <input
                id="file-input"
                type="file"
                name="name"
                style={{ display: "none" }}
                onChange={async (e) => {
                  const contents = base64ArrayBuffer(
                    (await e.target.files?.[0].arrayBuffer()) ??
                      new ArrayBuffer(0)
                  );
                  setOriginal(contents);
                  const res = await ky
                    .post("/api/process_async", {
                      json: { data: contents, prompts: [refine] },
                    })
                    .json<{ id: string }>();
                  setTaskId(res.id);
                }}
              />
              <Button
                colorScheme="teal"
                variant="ghost"
                onClick={() => document.getElementById("file-input")!.click()}
              >
                Select Image
              </Button>
            </CardBody>
          </Card>
        )}
        {original && (
          <Card width="100%">
            <CardBody>
              <Image src={"data:image;base64," + original} />
              <Text>{refine}</Text>
            </CardBody>
          </Card>
        )}
        {original && !redacted && (
          <CircularProgress isIndeterminate color="green.300" />
        )}
        {redacted && (
          <>
            <Card width="max-content">
              <CardBody>
                <Text>I have redacted the image based on your request:</Text>
              </CardBody>
            </Card>
            <Card width="100%">
              <CardBody>
                <Image src={"data:image;base64," + redacted} />
                <Text>Right click on the image to save it.</Text>
              </CardBody>
            </Card>
          </>
        )}
        {original && redacted && (
          <>
            <Card width="max-content">
              <CardBody>
                <Text>Do you want to refine the results?</Text>
              </CardBody>
            </Card>
            {/* <HStack>
              <Button colorScheme="teal" variant="ghost">
                Names
              </Button>
              <Button colorScheme="teal" variant="ghost">
                Address
              </Button>
              <Button colorScheme="teal" variant="ghost">
                Phone Number
              </Button>
            </HStack> */}
            <InputGroup size="md">
              <Input
                pr="4.5rem"
                placeholder="Type your request here"
                background="white"
                onChange={(e) => setRefine(e.target.value)}
              />
              <InputRightElement width="4.5rem">
                <Button
                  h="1.75rem"
                  size="sm"
                  onClick={async () => {
                    setRedacted("");
                    const res = await ky
                      .post("/api/process_async", {
                        json: { data: original, prompts: [refine] },
                      })
                      .json<{ id: string }>();
                    setTaskId(res.id);
                  }}
                >
                  Send
                </Button>
              </InputRightElement>
            </InputGroup>
          </>
        )}
      </Flex>
    </Container>
  );
}

export default App;
